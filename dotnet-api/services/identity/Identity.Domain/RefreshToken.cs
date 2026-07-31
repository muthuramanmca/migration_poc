namespace Identity.Domain;

/// <summary>
/// Placeholder entity supporting the plan's rotate/revoke refresh-token design (closing the gap
/// where the java-api baseline has a flat 1hr JWT with no refresh). Rotation/reuse-detection
/// behavior itself is deferred to Identity's business-logic pass.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
    public bool IsRevoked => RevokedAtUtc is not null;
}
