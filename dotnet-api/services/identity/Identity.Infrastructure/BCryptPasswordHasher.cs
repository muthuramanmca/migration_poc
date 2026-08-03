using Identity.Application;

namespace Identity.Infrastructure;

/// <summary>
/// BCrypt, not ASP.NET Core Identity's default PBKDF2 -- preserves spec rule 4.4 exactly and
/// keeps any real migrated password hash from java-api verifiable as-is (PBKDF2 would silently
/// invalidate every existing hash).
/// </summary>
public sealed class BCryptPasswordHasher : IPasswordHasher
{
    public string Hash(string plaintextPassword) => BCrypt.Net.BCrypt.EnhancedHashPassword(plaintextPassword);

    public bool Verify(string plaintextPassword, string passwordHash) =>
        BCrypt.Net.BCrypt.EnhancedVerify(plaintextPassword, passwordHash);
}
