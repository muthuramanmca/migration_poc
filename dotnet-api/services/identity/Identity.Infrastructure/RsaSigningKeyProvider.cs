using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace Identity.Infrastructure;

/// <summary>
/// Holds the RS256 signing key for this Identity instance and exposes the public JWKS every other
/// service validates tokens against. Skeleton: generates an ephemeral in-memory RSA key pair at
/// startup. A real implementation must load/persist/rotate a key (e.g. from a key vault) instead
/// of regenerating one on every restart -- that's deferred to Identity's business-logic pass.
/// </summary>
public sealed class RsaSigningKeyProvider : IDisposable
{
    private readonly RSA _rsa;

    public string KeyId { get; }
    public RsaSecurityKey SecurityKey { get; }
    public SigningCredentials SigningCredentials { get; }

    public RsaSigningKeyProvider()
    {
        _rsa = RSA.Create(2048);
        KeyId = Guid.NewGuid().ToString("N");
        SecurityKey = new RsaSecurityKey(_rsa) { KeyId = KeyId };
        SigningCredentials = new SigningCredentials(SecurityKey, SecurityAlgorithms.RsaSha256);
    }

    /// <summary>Public key only (modulus + exponent) -- never exposes the private key.</summary>
    public JsonWebKeySet GetPublicJwks()
    {
        var parameters = _rsa.ExportParameters(includePrivateParameters: false);

        var jwks = new JsonWebKeySet();
        jwks.Keys.Add(new JsonWebKey
        {
            Kty = "RSA",
            Use = "sig",
            Kid = KeyId,
            Alg = SecurityAlgorithms.RsaSha256,
            N = Base64UrlEncoder.Encode(parameters.Modulus),
            E = Base64UrlEncoder.Encode(parameters.Exponent),
        });

        return jwks;
    }

    public void Dispose() => _rsa.Dispose();
}
