namespace Identity.Application;

/// <summary>
/// MFA hook point wired into the login flow's shape now (skeleton); a real TOTP/SMS provider
/// implementation is deferred to Identity's business-logic pass. Default registration is
/// <c>NoOpMfaChallengeProvider</c> (Identity.Infrastructure).
/// </summary>
public interface IMfaChallengeProvider
{
    Task<bool> IsChallengeRequiredAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> VerifyChallengeAsync(Guid userId, string code, CancellationToken cancellationToken = default);
}
