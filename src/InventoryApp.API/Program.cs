using InventoryApp.Application;
using InventoryApp.Components;
using InventoryApp.Infrastructure;
using InventoryApp.Infrastructure.Persistence;
using InventoryApp.Web;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services
    .AddApplicationServices(builder.Configuration)
    .AddInfrastructureServices(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Interactive server rendering, for the dashboard's live session countdown. The pages that sign a
// user in or out stay static server-side rendering: cookies can only be written or deleted while a
// real HTTP response is in flight.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Makes the request's authentication state visible to components, which is how @Authorize and
// AuthorizeView read the UI cookie during server-side rendering.
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Inventory Management API",
        Version = "v1",
        Description = "Module 1: authentication, roles and policy-based authorization."
    });

    // Defines the "Authorize" button in the Swagger UI.
    options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the access token only, without the 'Bearer ' prefix."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = JwtBearerDefaults.AuthenticationScheme
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

app.UseExceptionHandler();

// The test host serves plain HTTP, so HTTPS redirection would bounce every integration test.
if (!app.Environment.IsEnvironment(TestingEnvironments.Name))
    app.UseHttpsRedirection();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Static assets for the UI stylesheet. Before routing, so it is not re-done per request path.
app.UseStaticFiles();

// Rate limiting runs before authentication so that a flood of credential requests is rejected
// without the cost of validating a token on each one.
app.UseRateLimiter();

app.UseSerilogRequestLogging();

app.UseAuthentication();
app.UseAuthorization();

// After authorization so the handler already knows who the caller is, and before the endpoints it
// protects. The UI sign-in endpoints disable automatic validation and check the token themselves, so
// a failure can redirect back to the form instead of returning a bare 400.
app.UseAntiforgery();

app.MapControllers();

// Form-post endpoints behind the UI's sign-in, register and sign-out screens.
app.MapUiEndpoints();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await app.Services.InitializeDatabaseAsync();

app.Run();

/// <summary>Exposed so WebApplicationFactory can boot this exact composition root from tests.</summary>
public partial class Program;