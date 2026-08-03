namespace Identity.Application;

public sealed record IssuedToken(string Token, long ExpiresInSeconds);

/// <summary>
/// Mints the JWTs every other service validates. Lives in Application as an interface so
/// IdentityService doesn't depend on Identity.Infrastructure's signing-key material directly.
/// </summary>
public interface IJwtTokenIssuer
{
    IssuedToken IssueToken(string username, string role);
}
