using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;

namespace Inventory.Api.Extensions;

/// <summary>
/// Builds one Swagger document per API version, so the docs cannot drift from the
/// routes that are actually registered.
/// </summary>
public sealed class ConfigureSwaggerOptions(IApiVersionDescriptionProvider provider) : IConfigureOptions<Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions>
{
    public void Configure(Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions options)
    {
        foreach (var description in provider.ApiVersionDescriptions)
        {
            options.SwaggerDoc(description.GroupName, new OpenApiInfo
            {
                Title = "Inventory Management API",
                Version = description.GroupName,
                Description = "Stock control, purchasing and sales ordering.",
            });
        }
    }
}