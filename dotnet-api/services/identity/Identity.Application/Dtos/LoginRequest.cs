namespace Identity.Application.Dtos;

/// <summary>No password-strength check here (spec rule 4.3 applies to registration only) -- just presence.</summary>
public sealed record LoginRequest(string Username, string Password);
