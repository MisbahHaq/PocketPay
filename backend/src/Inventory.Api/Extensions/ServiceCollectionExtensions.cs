using System.Threading.RateLimiting;
using Asp.Versioning;
using Inventory.Application.Abstractions;
using Inventory.Application.Common;
using Inventory.Domain.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace Inventory.Api.Extensions;

/// <summary>Service registration for the API layer.</summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddControllers()
            .AddJsonOptions(options =>
            {
                // camelCase throughout, matching the TypeScript client. Configure it here
                // rather than per-property so no DTO can drift from the convention.
                options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;

                // Enums as strings, so "StockMovementType.Receipt" is never sent as a
                // magic number that a reordered enum would silently re-map.
                options.JsonSerializerOptions.Converters.Add(
                    new System.Text.Json.Serialization.JsonStringEnumConverter());
            });

        services.AddProblemDetails();

        services.AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1, 0);

                // A client that sends no version gets the current one instead of a 400.
                // An API with one live version should not break because a header is missing.
                options.AssumeDefaultVersionWhenUnspecified = true;

                // Clients must opt in to unknown versions. Guessing means a typo'd or
                // malicious "v99" silently gets today's behaviour.
                options.ReportApiVersions = true;
            })
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            });

        services.AddSwaggerGen(options => AddBearerAuthentication(options));

        // One document per version, built from the live route table.
        services.ConfigureOptions<ConfigureSwaggerOptions>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options => ConfigureJwtBearer(options, configuration));

        services.AddAuthorization(options =>
        {
            // Deny by default. A forgotten [Authorize] then yields a 401 that tests
            // catch, rather than silently exposing an endpoint to the internet.
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();

            AddPolicy(options, AuthorizationPolicies.ManageUsers, SystemRoles.Admin);
            AddPolicy(options, AuthorizationPolicies.ManageCatalog, SystemRoles.Admin, SystemRoles.Manager);
            AddPolicy(options, AuthorizationPolicies.DeleteCatalog, SystemRoles.Admin);
            AddPolicy(options, AuthorizationPolicies.RecordMovements, SystemRoles.Admin, SystemRoles.Manager, SystemRoles.Staff);
            AddPolicy(options, AuthorizationPolicies.ApproveStock, SystemRoles.Admin, SystemRoles.Manager);
            AddPolicy(options, AuthorizationPolicies.ManageOrders, SystemRoles.Admin, SystemRoles.Manager);
            AddPolicy(options, AuthorizationPolicies.CancelOrders, SystemRoles.Admin, SystemRoles.Manager);
            AddPolicy(options, AuthorizationPolicies.ViewReports, SystemRoles.Admin, SystemRoles.Manager, SystemRoles.Staff);
            AddPolicy(options, AuthorizationPolicies.ViewAuditLog, SystemRoles.Admin);
        });

        services.AddCors(options => AddCorsPolicy(options, configuration));

        services.AddRateLimiter(options => AddRateLimiting(options, configuration));

        services.AddResponseCompression();

        return services;
    }

    private static void ConfigureJwtBearer(JwtBearerOptions options, IConfiguration configuration)
    {
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("The 'Jwt' configuration section is missing.");

        if (string.IsNullOrWhiteSpace(jwt.SigningKey) || jwt.SigningKey.Length < 32)
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey must be at least 32 characters. Provide it via user secrets "
                + "(dotnet user-secrets set \"Jwt:SigningKey\" <value>) or the Jwt__SigningKey "
                + "environment variable.");
        }

        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidateLifetime = true,

            // 30s tolerance absorbs clock drift between the API host and the database
            // host without meaningfully widening the usable window on a 15-minute token.
            ClockSkew = TimeSpan.FromSeconds(30),

            NameClaimType = System.Security.Claims.ClaimTypes.Name,
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
        };

        // Return RFC 7807 instead of an empty 401 body, so the frontend has one error
        // shape to parse whether the failure came from the handler or from the auth
        // middleware.
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();

                if (context.Response.HasStarted)
                {
                    return;
                }

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/problem+json";

                await context.Response.WriteAsJsonAsync(new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "Authentication is required.",
                    Type = "https://httpstatuses.io/401",
                    Detail = "The access token is missing, expired or invalid.",
                    Instance = context.HttpContext.Request.Path,
                    Extensions = { ["code"] = "unauthenticated" },
                });
            },
            OnForbidden = async context =>
            {
                if (context.Response.HasStarted)
                {
                    return;
                }

                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/problem+json";

                await context.Response.WriteAsJsonAsync(new ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "You do not have permission to perform this action.",
                    Type = "https://httpstatuses.io/403",
                    Instance = context.HttpContext.Request.Path,
                    Extensions = { ["code"] = "forbidden" },
                });
            },
        };
    }

    private static void AddBearerAuthentication(Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions options)
    {
        var scheme = new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Paste the access token only. Do not include the 'Bearer ' prefix.",
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
        };

        options.AddSecurityDefinition("Bearer", scheme);

        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            [scheme] = Array.Empty<string>(),
        });
    }

    private static void AddPolicy(AuthorizationOptions options, string policy, params string[] roles) =>
        options.AddPolicy(policy, policyBuilder => policyBuilder
            .RequireAuthenticatedUser()
            .RequireRole(roles));

    private static void AddCorsPolicy(CorsOptions options, IConfiguration configuration)
    {
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        // No wildcard fallback. This is a bearer-token API, and combining
        // AllowAnyOrigin with credentials is both rejected by the framework and the
        // classic configuration that invites CSRF. Origins are explicit configuration.
        options.AddPolicy("spa", policyBuilder => policyBuilder
            .WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .WithExposedHeaders("Content-Disposition"));

        if (origins.Length == 0)
        {
            // Surfaced at startup rather than as a confusing browser CORS failure later.
            throw new InvalidOperationException(
                "Cors:AllowedOrigins is empty. Add the frontend origin, e.g. \"http://localhost:5173\", "
                + "or set Cors__AllowedOrigins__0 in the environment.");
        }
    }

    private static void AddRateLimiting(RateLimiterOptions options, IConfiguration configuration)
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        // A token bucket smooths bursts: ten operators hitting "save" when a truck
        // arrives should not be blocked by a fixed window that resets on the minute.
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
            httpContext =>
            {
                var partitionKey = ResolveClientKey(httpContext);
                return RateLimitPartition.GetTokenBucketLimiter(
                    partitionKey,
                    _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = 120,
                        TokensPerPeriod = 120,
                        ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true,
                    });
            });

        options.AddPolicy(RateLimitPolicies.Login, httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                ResolveClientKey(httpContext),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                }));

        options.AddPolicy(RateLimitPolicies.Refresh, httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                ResolveClientKey(httpContext),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 60,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));

        options.OnRejected = async (context, cancellationToken) =>
        {
            if (context.HttpContext.Response.HasStarted)
            {
                return;
            }

            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.HttpContext.Response.ContentType = "application/problem+json";

            await context.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Too many requests.",
                Type = "https://httpstatuses.io/429",
                Detail = "Slow down and try again shortly.",
                Instance = context.HttpContext.Request.Path,
                Extensions =
                {
                    ["code"] = "rate-limit-exceeded",
                    ["retryAfterSeconds"] = 60,
                },
            }, cancellationToken);
        };
    }

    /// <summary>
    /// Identifies the caller for rate-limiting purposes.
    /// </summary>
    /// <remarks>
    /// Behind a reverse proxy the remote address is the proxy itself, so without this
    /// every user would share one bucket and the first busy user would lock out
    /// everyone. The forwarded header is only trusted because the API is expected to sit
    /// behind a proxy that overwrites it; if it were exposed directly, a caller could
    /// spoof the header to evade the limit.
    /// </remarks>
    private static string ResolveClientKey(HttpContext httpContext)
    {
        var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            var first = forwardedFor.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(first))
            {
                return first;
            }
        }

        return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}