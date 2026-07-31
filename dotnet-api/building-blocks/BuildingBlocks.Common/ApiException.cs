using Microsoft.AspNetCore.Http;

namespace BuildingBlocks.Common;

/// <summary>
/// Exception carrying an <see cref="ApiError"/> plus the HTTP status it should map to.
/// Thrown from Application-layer code; translated into a response by <see cref="GlobalExceptionMiddleware"/>.
/// </summary>
public class ApiException : Exception
{
    public int StatusCode { get; }
    public ApiError Error { get; }

    public ApiException(int statusCode, string code, string message, IReadOnlyDictionary<string, object?>? details = null)
        : base(message)
    {
        StatusCode = statusCode;
        Error = new ApiError(code, message, details);
    }

    public static ApiException NotFound(string code, string message) => new(StatusCodes.Status404NotFound, code, message);

    public static ApiException Conflict(string code, string message) => new(StatusCodes.Status409Conflict, code, message);

    public static ApiException BadRequest(string code, string message, IReadOnlyDictionary<string, object?>? details = null) =>
        new(StatusCodes.Status400BadRequest, code, message, details);

    public static ApiException Unauthorized(string code, string message) => new(StatusCodes.Status401Unauthorized, code, message);

    public static ApiException Forbidden(string code, string message) => new(StatusCodes.Status403Forbidden, code, message);
}
