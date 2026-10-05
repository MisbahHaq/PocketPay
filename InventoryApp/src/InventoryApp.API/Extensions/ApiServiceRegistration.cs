using InventoryApp.Application.Auth;
using InventoryApp.Application.Authorization;
using InventoryApp.Domain.Constants;
using InventoryApp.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace InventoryApp.API.Extensions;

public static class ApiServiceRegistration
{
    /// <summary>
    /// Registers the HTTP-facing concerns: controllers, authentication scheme,
    /// authorization policies and rate limiting.
    /// </summary>
    public static IServiceCollection AddApiLayer(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddProblemDetails();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        // Configured through DI so the bearer handler uses the very same
        // validated JwtOptions instance the infrastructure layer registered.
        services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearerOptions, jwtOptions) =>
                ConfigureJwtBearer(bearerOptions, jwtOptions.Value));

        services.AddAuthorization(AddPolicies);
        services.AddRateLimiter(RateLimitingConfiguration.Configure);

        services
            .AddControllers()
            .ConfigureApiBehaviorOptions(options =>
                // Throw instead of returning MVC's default 400 so that every
                // error response is produced by the same middleware.
                options.InvalidModelStateResponseFactory = context =>
                    throw new Application.Common.Exceptions.ValidationFailedException(
                        context.ModelState
                            .Where(entry => entry.Value is { Errors.Count: > 0 })
                            .ToDictionary(
                                entry => entry.Key,
                                entry => entry.Value!.Errors
                                    .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                                        ? "The value provided is not valid."
                                        : error.ErrorMessage)
                                    .ToArray())));

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(ConfigureSwagger);

        return services;
    }

    private static void ConfigureJwtBearer(JwtBearerOptions options, JwtOptions jwtOptions)
    {
        // Read claims exactly as they were written, so "sub" and "role" stay
        // "sub" and "role" instead of being rewritten to long claim URIs.
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(jwtOptions.ClockSkewSeconds),
            RoleClaimType = AuthClaimTypes.Role,
            NameClaimType = AuthClaimTypes.UserId,
            RequireExpirationTime = true,
        };

        // Never redirect to a login page: this is an API, so an anonymous request
        // must get a plain 401 that the Swagger "Authorize" button can act on.
        options.Events = new JwtBearerEvents
        {
            OnChallenge = context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;

                return context.Response.WriteAsJsonAsync(new
                {
                    type = "https://tools.ietf.org/html/rfc6750#section-3.1",
                    title = "Unauthorized",
                    status = StatusCodes.Status401Unauthorized,
                    detail = "A valid bearer token is required to call this endpoint.",
                    traceId = context.HttpContext.TraceIdentifier,
                });
            },
        };
    }

    private static void AddPolicies(AuthorizationOptions options)
    {
        // Endpoints declare a policy, never a role. The rule "who may do this"
        // therefore lives in one place: changing it is a single edit here instead
        // of finding every controller that mentions the role.
        options.AddPolicy(
            AuthorizationPolicies.CanManageUsers,
            policy => policy.RequireRole(ApplicationRoles.Admin, ApplicationRoles.Manager));

        options.AddPolicy(
            AuthorizationPolicies.CanApproveAdjustments,
            policy => policy.RequireRole(ApplicationRoles.Admin, ApplicationRoles.Manager));

        options.AddPolicy(
            AuthorizationPolicies.CanRecordMovements,
            policy => policy.RequireRole(
                ApplicationRoles.Admin,
                ApplicationRoles.Manager,
                ApplicationRoles.Staff));

        // When a rule stops being "does the user hold one of these roles" -
        // say "Managers may approve adjustments up to 5,000" or "the caller
        // must belong to the same warehouse" - only this file changes, by
        // switching RequireRole for RequireAssertion or a custom
        // IAuthorizationRequirement. No controller or endpoint is touched.
    }

    private static void ConfigureSwagger(Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions options)
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Inventory Management API",
            Version = "v1",
            Description = "Module 1 - Authentication and roles.",
        });

        var scheme = new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Paste the access token only, without the 'Bearer ' prefix.",
            Reference = new OpenApiReference
            {
                Type = ReferenceType.SecurityScheme,
                Id = "Bearer",
            },
        };

        options.AddSecurityDefinition("Bearer", scheme);

        // Applied globally so Swagger UI shows the "Authorize" button on every
        // endpoint, including the anonymous login endpoint.
        options.AddSecurityRequirement(new OpenApiSecurityRequirement { [scheme] = [] });
    }
}
