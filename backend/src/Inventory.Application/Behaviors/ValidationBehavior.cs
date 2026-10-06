using FluentValidation;
using Inventory.Application.Common;
using MediatR;
using ValidationBehaviour = Inventory.Application.Common.ApplicationValidationException;

namespace Inventory.Application.Behaviors;

/// <summary>
/// Runs every FluentValidation validator registered for the request before the handler
/// sees it.
/// </summary>
/// <remarks>
/// <para>
/// Validating in the pipeline rather than in each handler means a new command cannot
/// forget to validate - the failure mode is uniform (422 + a field-keyed error list)
/// and the handler is written as if the input is already known good.
/// </para>
/// <para>
/// This complements rather than replaces model binding. <c>[ApiController]</c> turns
/// bad model state into a 400 for shape problems (a non-numeric id); this catches
/// everything ASP.NET cannot know, including rules that need the database.
/// </para>
/// </remarks>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IValidator<TRequest>[] _validators = [.. validators];

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (_validators.Length == 0)
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);

        var results = await Task.WhenAll(
            _validators.Select(validator => validator.ValidateAsync(context, cancellationToken)));

        var failures = results
            .SelectMany(result => result.Errors)
            .Where(failure => failure is not null)
            .ToArray();

        if (failures.Length > 0)
        {
            throw ValidationBehaviour.FromFluentResults(failures);
        }

        return await next();
    }
}