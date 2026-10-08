using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using server.Authorization;
using server.Data;
using server.Helpers;
using server.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
        options.JsonSerializerOptions.WriteIndented = true;
        options.JsonSerializerOptions.ReferenceHandler =
      System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles ;
    });

builder.Services.AddOpenApi();
builder.Services.AddCors(options => { options.AddPolicy("AllowReact", policy => { policy.WithOrigins("http://localhost:5173", "http://localhost:5174").AllowAnyHeader().AllowAnyMethod(); }); });

builder.Services.AddScoped<JwtHelper>();

//db registering
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultString")));

builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection("JwtSettings"));

var jwtSettings = builder.Configuration
    .GetSection("JwtSettings")
    .Get<JwtSettings>();


//JWT Verification Service
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer= jwtSettings!.Issuer,
            ValidAudience= jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings!.SecretKey))
        };
    });

//current user / audit trail / permission-based authorization
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUserService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();
builder.Services.AddScoped<IAuthorizationHandler, OrgMemberHandler>();

builder.Services.AddAuthorization(options =>
{
    foreach (var (key, _) in PermissionKeys.All)
    {
        options.AddPolicy(key, policy => policy.Requirements.Add(new PermissionRequirement(key)));
    }

    options.AddPolicy(PolicyNames.OrgMember, policy => policy.Requirements.Add(new OrgMemberRequirement()));
});

var app = builder.Build();


//using DbSeeder file to seed data into the database
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    await DbSeeder.SeedAsync(context);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference("/scalar");
}

// app.UseHttpsRedirection();
app.UseCors("AllowReact");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

