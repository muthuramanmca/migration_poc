using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Common;

/// <summary>
/// Wires FluentValidation into Minimal API endpoints -- there's no [ApiController]-style automatic
/// model validation here, so this is the one place that translates a failed IValidator&lt;T&gt;
/// into the same VALIDATION_FAILED envelope every service already returns via ApiException/
/// GlobalExceptionMiddleware. Registered per-DTO via <see cref="RouteHandlerBuilderExtensions.WithValidation{T}"/>.
/// </summary>
public sealed class ValidationFilter<T> : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var argument = context.Arguments.OfType<T>().FirstOrDefault();
        if (argument is null)
        {
            return await next(context);
        }

        var validator = context.HttpContext.RequestServices.GetService<IValidator<T>>();
        if (validator is null)
        {
            return await next(context);
        }

        var result = await validator.ValidateAsync(argument);
        if (!result.IsValid)
        {
            var fieldErrors = result.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}").ToArray();
            throw ApiException.BadRequest(
                "VALIDATION_FAILED",
                "Request validation failed",
                new Dictionary<string, object?> { ["fieldErrors"] = fieldErrors });
        }

        return await next(context);
    }
}
