namespace Identity.Application;

/// <summary>Abstraction over the hashing algorithm so IdentityService isn't coupled to a specific library.</summary>
public interface IPasswordHasher
{
    string Hash(string plaintextPassword);

    bool Verify(string plaintextPassword, string passwordHash);
}
