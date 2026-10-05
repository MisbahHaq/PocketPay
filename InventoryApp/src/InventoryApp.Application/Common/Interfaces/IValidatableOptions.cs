namespace InventoryApp.Application.Common.Interfaces;

/// <summary>
/// Implemented by options classes that can describe their own configuration
/// problems, so the composition root can fail fast with a precise message
/// instead of surfacing an obscure failure at the first request.
/// </summary>
public interface IValidatableOptions
{
    /// <summary>Returns one message per problem; empty when the configuration is usable.</summary>
    IReadOnlyList<string> Validate();
}
