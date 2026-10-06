using System.Reflection;
using FluentValidation;
using Inventory.Application.Abstractions;
using Inventory.Application.Behaviors;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Inventory.Application;

/// <summary>
/// Composition root for the Application layer.
/// </summary>
public static class DependencyInjection
{
    /// <summary>Assembly marker used to auto-discover MediatR handlers and validators.</summary>
    public static Assembly Assembly => typeof(DependencyInjection).Assembly;

    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly;

        // MediatR scans once at startup, so a forgotten handler fails immediately and
        // visibly rather than at the first request that needs it.
        services.AddMediatR(configuration => configuration.RegisterServicesFromAssembly(assembly));

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        // Order matters. Validation runs first so a bad request is rejected before any
        // logging, timing or event-dispatch work happens; domain events are dispatched
        // last, once the transaction has committed.
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(DomainEventDispatchBehavior<,>));

        return services;
    }
}