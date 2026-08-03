namespace Identity.Application.Dtos;

public sealed record AuthResponse(string Token, long ExpiresInSeconds);
