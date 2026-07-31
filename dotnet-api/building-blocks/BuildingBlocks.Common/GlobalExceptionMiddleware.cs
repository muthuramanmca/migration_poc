using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Common;

/// <summary>
/// Central place every unhandled exception (or explicit <see cref="ApiException"/>) turns into an
/// <see cref="ApiError"/> JSON response. Mirrors java-api's GlobalExceptionHandler so the .NET rewrite
/// keeps the same error-envelope contract for API consumers.
/// </summary>
public sealed class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ApiException ex)
        {
            logger.LogWarning(ex, "Request failed with {Code}: {Message}", ex.Error.Code, ex.Error.Message);
            await WriteErrorAsync(context, ex.StatusCode, ex.Error);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception processing {Method} {Path}", context.Request.Method, context.Request.Path);
            var error = new ApiError("INTERNAL_ERROR", "An unexpected error occurred.");
            await WriteErrorAsync(context, StatusCodes.Status500InternalServerError, error);
        }
    }

    private static Task WriteErrorAsync(HttpContext context, int statusCode, ApiError error)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsync(JsonSerializer.Serialize(error));
    }
}
