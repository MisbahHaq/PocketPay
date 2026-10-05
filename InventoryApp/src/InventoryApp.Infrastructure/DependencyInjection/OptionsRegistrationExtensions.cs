using InventoryApp.Application.Common.Exceptions;
using InventoryApp.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace InventoryApp.Infrastructure.DependencyInjection;

internal static class OptionsRegistrationExtensions
{
    /// <summary>
    /// Binds an options section once, validates it immediately and registers the
    /// result as a singleton <see cref="IOptions{TOptions}"/>.
    ///
    /// Validation happens here, while the host is still being composed, so a
    /// missing signing key or a too-short secret stops the application from
    /// starting at all rather than failing on the first login attempt.
    /// </summary>
    public static TOptions AddValidatedOptions<TOptions>(
        this IServiceCollection services,
        IConfiguration section,
        string optionsName)
        where TOptions : class, IValidatableOptions, new()
    {
        var instance = section.Get<TOptions>() ?? new TOptions();
        var problems = instance.Validate();

        if (problems.Count > 0)
        {
            throw new ConfigurationException(
                $"Configuration section '{optionsName}' is invalid:{Environment.NewLine}  - "
                + string.Join(Environment.NewLine + "  - ", problems));
        }

        services.AddSingleton(Options.Create(instance));
        return instance;
    }
}
