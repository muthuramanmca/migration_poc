using Identity.Application;

namespace Identity.Infrastructure;

/// <summary>Default registration: never challenges. Swap for a real TOTP/SMS provider when MFA is actually implemented.</summary>
public class NoOpMfaChallengeProvider : IMfaChallengeProvider
{
    public Task<bool> IsChallengeRequiredAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task<bool> VerifyChallengeAsync(Guid userId, string code, CancellationToken cancellationToken = default) =>
        Task.FromResult(true);
}
