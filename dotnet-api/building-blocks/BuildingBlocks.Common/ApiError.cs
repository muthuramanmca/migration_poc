namespace BuildingBlocks.Common;

/// <summary>
/// Uniform error envelope returned by every service for non-2xx responses.
/// Mirrors the java-api ApiError shape so client-facing error contracts stay stable across the migration.
/// </summary>
public sealed record ApiError(string Code, string Message, IReadOnlyDictionary<string, object?>? Details = null);
