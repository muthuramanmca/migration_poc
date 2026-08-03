using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace BuildingBlocks.Common;

public static class RouteHandlerBuilderExtensions
{
    /// <summary>Runs <see cref="ValidationFilter{T}"/> against the endpoint's <typeparamref name="T"/> argument before the handler executes.</summary>
    public static RouteHandlerBuilder WithValidation<T>(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter<RouteHandlerBuilder, ValidationFilter<T>>();
}
