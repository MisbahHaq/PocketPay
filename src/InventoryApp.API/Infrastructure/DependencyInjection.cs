using System.Text;
using System.Threading.RateLimiting;
using InventoryApp.Application.Abstractions;
using InventoryApp.Application.Common;
using InventoryApp.Application.Common.Options;
using InventoryApp.Domain.Entities;
using InventoryApp.Domain.Security;
using InventoryApp.Infrastructure.Errors;
using InventoryApp.Infrastructure.Persistence;
using InventoryApp.Infrastructure.Services;
using InventoryApp.Web;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace InventoryApp.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registers everything technology-specific: EF Core, Identity, JWT bearer authentication,
    /// authorization policies, rate limiting and the implementations of the Application abstractions.
    /// </summary>
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connectionString));

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                // Single source of truth for password strength: RegisterUserRequestValidator reads
                // these same values, so the rules cannot drift between the two layers.
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequiredUniqueChars = 4;

                // Lockout is on by default: IdentityUser.LockoutEnabled is true and the EF store persists the
// lockout end stamp, so no extra enable flag is needed here.
options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        services.AddScoped<IPasswordHasher<ApplicationUser>, PasswordHasher<ApplicationUser>>();

        services.Configure<JwtTokenOptions>(configuration.GetSection(JwtTokenOptions.SectionName));

        // Read from configuration on demand rather than once here. An options callback runs the first
        // time the scheme's options are resolved, which is after the host has finished building and
        // after any test host has contributed its configuration. Reading eagerly at registration time
        // would validate tokens against the appsettings placeholders instead, because the test host's
        // signing key is added to configuration only after this method has returned.
        JwtTokenOptions ReadJwtOptions() =>
            configuration.GetSection(JwtTokenOptions.SectionName).Get<JwtTokenOptions>()
            ?? throw new InvalidOperationException("The Jwt configuration section is missing.");

        services.AddAuthentication(options =>
            {
                // The UI is cookie-based and the API is bearer-only. A selector scheme picks the
                // right handler per request so that one default setting can serve both surfaces,
                // leaving every existing [Authorize] on a controller correct without being rewritten.
                options.DefaultAuthenticateScheme = AuthSchemes.Selector;
                options.DefaultChallengeScheme = AuthSchemes.Selector;

                // Signing in and out are browser-only operations, so they name the cookie directly
                // rather than going through a selector that would reject a scheme request.
                options.DefaultSignInScheme = AuthSchemes.UiCookie;
                options.DefaultSignOutScheme = AuthSchemes.UiCookie;
            })
            .AddPolicyScheme(AuthSchemes.Selector, "API bearer tokens for /api, UI cookie elsewhere", options =>
            {
                options.ForwardDefaultSelector = context =>
                    context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
                        ? JwtBearerDefaults.AuthenticationScheme
                        : AuthSchemes.UiCookie;
            })
            .AddJwtBearer(options =>
            {
                var jwt = ReadJwtOptions();

                // Claims are written and read under their standard short names, so the framework's static
                // ClaimTypes URI mapping is switched off rather than relied upon.
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,

                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,

                    ValidateLifetime = true,

                    // The signature check is what makes the claims trustworthy: without it a caller
                    // could hand us an unsigned token containing the Admin role.
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),

                    // Every validation must be on, or an unverified claim leaks through.
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,

                    RoleClaimType = AuthClaimTypes.Role,
                    NameClaimType = AuthClaimTypes.Name,

                    // Tokens are short-lived, so tightening the default 5-minute tolerance costs
                    // nothing and shortens the window in which a dead token still works.
                    ClockSkew = TimeSpan.FromSeconds(30)
                };

                // No cookie, no antiforgery token: /api is a pure bearer-token surface. The UI's own
                // cookie and antiforgery token are a separate concern, on the separate cookie scheme.
                options.Events = new JwtBearerEvents
                {
                    // The default bearer challenge returns 401 with an empty body, which would make
                    // authentication failures the only errors clients cannot parse as ProblemDetails.
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();

                        if (context.Response.HasStarted)
                            return;

                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;

                        if (!context.Response.Headers.ContainsKey("WWW-Authenticate"))
                            context.Response.Headers.WWWAuthenticate = context.Options.Challenge;

                        await ProblemDetailsWriter.WriteAsync(
                            context.HttpContext,
                            StatusCodes.Status401Unauthorized,
                            "Unauthorized",
                            "A valid access token is required. Sign in or refresh your token.");
                    },

                    OnForbidden = async context =>
                    {
                        if (context.Response.HasStarted)
                            return;

                        await ProblemDetailsWriter.WriteAsync(
                            context.HttpContext,
                            StatusCodes.Status403Forbidden,
                            "Forbidden",
                            "Your roles do not permit this operation.");
                    },

                    OnMessageReceived = context =>
                    {
                        // Read the token from the query string only for SignalR-style transports,
                        // which cannot set headers.
                        var accessToken = context.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(accessToken)
                            && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    }
                };
            })
            .AddCookie(AuthSchemes.UiCookie, options =>
            {
                var jwt = ReadJwtOptions();

                // Named and versioned, so a change to the ticket layout invalidates in-flight UI
                // sessions instead of failing to deserialize them.
                options.Cookie.Name = "InventoryApp.Ui";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;

                // Lax, not Strict: Strict would drop the cookie on the navigation that follows an
                // external sign-in link, so the user would land back on the login page signed out.
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;

                // Deliberately short, and not a sliding session. The cookie authorises the browser,
                // so its lifetime is tied to the access token it was minted from rather than being
                // allowed to outlive the credentials it represents.
                options.ExpireTimeSpan = TimeSpan.FromMinutes(jwt.AccessTokenMinutes);
                options.SlidingExpiration = false;

                options.LoginPath = UiEndpoints.LoginPage;
                options.AccessDeniedPath = UiEndpoints.LoginPage;
                options.ReturnUrlParameter = "returnUrl";

                // The default handlers redirect with a 302 that carries the rejected URL in the
                // query string. Announcing the reason in the cookie is enough for a page that can
                // render it, and avoids leaking the attempted URL into browser history and logs.
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.Redirect(BuildLoginRedirect(context.Request.Path));
                    return Task.CompletedTask;
                };

                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.Redirect(BuildLoginRedirect(context.Request.Path));
                    return Task.CompletedTask;
                };

                options.Events.OnValidatePrincipal = context =>
                {
                    // The cookie is a copy of claims that were current at sign-in. A user who is
                    // deactivated or has a role change keeps those claims until the cookie expires,
                    // which is the same window their access token already has. Checking the
                    // database on every request would be stricter, and would also mean a database
                    // round trip on every page render.
                    return Task.CompletedTask;
                };
            });

        services.AddAuthorization(options =>
        {
            // Policies, not role strings in attributes: the requirement is named and centralised here,
            // and adding a role later is a one-line change in this file.
            options.AddPolicy(AuthorizationPolicies.CanManageUsers, policy =>
                policy.RequireRole(SystemRoles.Admin));

            options.AddPolicy(AuthorizationPolicies.CanApproveAdjustments, policy =>
                policy.RequireRole(SystemRoles.Admin, SystemRoles.Manager));

            options.AddPolicy(AuthorizationPolicies.CanRecordMovements, policy =>
                policy.RequireRole(SystemRoles.Admin, SystemRoles.Manager, SystemRoles.Staff));
        });

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            var limits = configuration.GetSection(RateLimitOptions.SectionName).Get<RateLimitOptions>()
                ?? new RateLimitOptions();

            // Per-IP fixed windows. Behind a reverse proxy RemoteIpAddress is the proxy's address, so
            // the host must enable forwarded headers for this to be per-client rather than per-proxy.
            Func<HttpContext, RateLimitPartition<string>> PerRemoteIp(int permitLimit) =>
                httpContext => RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = TimeSpan.FromSeconds(limits.WindowSeconds),
                        QueueLimit = 0,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                    });

            options.AddPolicy(RateLimitPolicies.Login, PerRemoteIp(limits.LoginPermitLimit));
            options.AddPolicy(RateLimitPolicies.Refresh, PerRemoteIp(limits.RefreshPermitLimit));

            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();

                // Matches the shape produced by the global exception handler so clients only ever
                // have to understand one error contract.
                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Too many requests",
                    Detail = "Rate limit exceeded. Retry after the indicated delay.",
                    Instance = context.HttpContext.Request.Path
                };
                problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

                await context.HttpContext.Response.WriteAsJsonAsync(
                    problem,
                    options: null,
                    contentType: "application/problem+json",
                    cancellationToken);
            };
        });

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, HttpContextCurrentUserService>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<DatabaseSeeder>();
        services.AddScoped<UiNoticeService>();
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();

        return services;
    }

    /// <summary>
    /// Builds the sign-in redirect for the UI cookie handler, preserving where the user was headed
    /// as a relative path. The endpoint re-validates this before honouring it.
    /// </summary>
    private static string BuildLoginRedirect(PathString originalPath) =>
        string.IsNullOrEmpty(originalPath)
            ? UiEndpoints.LoginPage
            : $"{UiEndpoints.LoginPage}?returnUrl={Uri.EscapeDataString(originalPath.Value!)}";
}