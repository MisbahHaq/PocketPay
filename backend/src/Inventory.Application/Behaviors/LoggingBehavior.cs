using System.Diagnostics;
using Inventory.Application.Abstractions;
using Inventory.Domain.Common;
using MediatR;

namespace Inventory.Application.Behaviors;

/// <summary>
/// Logs one structured entry per use case: name, elapsed time, and outcome.
/// </summary>
/// <remarks>
/// <para>
/// Note what is deliberately <em>not</em> logged: the request object itself. Commands
/// are records, so <c>ToString()</c> renders every property - which would write
/// plaintext passwords and raw refresh tokens into the log. Only the type name is
/// logged, plus an explicit property allow-list per command where a value genuinely
/// helps debugging.
/// </para>
/// <para>
/// The trace id is attached by Serilog's <c>LogContext</c> from the Activity, so a
/// client-reported failure can be found by a single id.
/// </para>
/// </remarks>
public sealed class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger,
    ICurrentUserService currentUser)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var name = typeof(TRequest).Name;

        logger.LogInformation(
            "Handling {RequestName} for user {UserId}",
            name,
            currentUser.UserId?.ToString() ?? "anonymous");

        var started = Stopwatch.GetTimestamp();

        try
        {
            var response = await next();

            logger.LogInformation(
                "Handled {RequestName} in {ElapsedMs} ms",
                name,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds);

            return response;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Failed {RequestName} after {ElapsedMs} ms",
                name,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds);

            throw;
        }
    }
}

/// <summary>
/// Dispatches domain events after the unit of work has committed.
/// </summary>
/// <remarks>
/// <para>
/// Registered in the pipeline after <c>SaveChangesAsync</c> succeeds, not before. If
/// events were published inside the transaction, a handler that threw would roll back
/// work the caller had already been told succeeded, and a commit that failed afterwards
/// would have announced a change that never happened. For an inventory system, where
/// "low stock" alerts and reorder suggestions are driven by these events, announcing
/// them speculatively would generate a stream of phantom reorder emails.
/// </para>
/// <para>
/// The trade-off is that a handler that fails leaves the write committed and the
/// event unprocessed. That is the right way round: the write is the source of truth,
/// and the event is a notification derived from it. Later phases add an outbox table
/// to make delivery retryable if that guarantee turns out to matter.
/// </para>
/// </remarks>
public sealed class DomainEventDispatchBehavior<TRequest, TResponse>(
    IPublisher publisher,
    IUnitOfWork unitOfWork)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var response = await next();

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var events = unitOfWork.CollectPendingDomainEvents();

        foreach (var domainEvent in events)
        {
            await publisher.Publish(domainEvent, cancellationToken);
        }

        return response;
    }
}