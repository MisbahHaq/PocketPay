using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace WalletPlatform.Api.Validation;

/// <summary>
/// Runs the FluentValidation validator registered for each action argument and
/// short-circuits with a standard ValidationProblemDetails response on failure.
/// </summary>
public sealed class ValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            var validationResult = await ValidateAsync(argument, context.HttpContext);
            if (validationResult.IsValid)
            {
                continue;
            }

            var errors = validationResult.Errors
                .GroupBy(failure => JsonNamingPolicy.CamelCase.ConvertName(failure.PropertyName))
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(failure => failure.ErrorMessage).ToArray());

            var problem = new ValidationProblemDetails(errors)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "One or more validation errors occurred.",
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                Instance = context.HttpContext.Request.Path,
            };

            context.Result = new ObjectResult(problem)
            {
                StatusCode = StatusCodes.Status400BadRequest,
                ContentTypes = { "application/problem+json" },
            };
            return;
        }

        await next();
    }

    private static async Task<ValidationResult> ValidateAsync(object instance, HttpContext httpContext)
    {
        var validatorType = typeof(IValidator<>).MakeGenericType(instance.GetType());
        if (httpContext.RequestServices.GetService(validatorType) is not IValidator validator)
        {
            return new ValidationResult();
        }

        var validationContextType = typeof(ValidationContext<>).MakeGenericType(instance.GetType());
        var validationContext = (IValidationContext)Activator.CreateInstance(validationContextType, instance)!;

        return await validator.ValidateAsync(validationContext, httpContext.RequestAborted);
    }
}
