using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Identity.Application;
using Microsoft.Extensions.Configuration;

namespace Identity.Infrastructure;

/// <summary>
/// Mints RS256-signed JWTs using this instance's own signing key (<see cref="RsaSigningKeyProvider"/>)
/// -- the public half is exactly what /.well-known/jwks.json serves, so every service validating
/// against this issuer (this one included, once it's a resource server) can verify the signature.
/// Issuer/audience/expiry are config-driven, matching how every other cross-service setting in
/// this solution already works -- not hardcoded the way java-api's JwtService.EXPIRY_SECONDS was.
/// </summary>
public sealed class JwtTokenIssuer(RsaSigningKeyProvider signingKeyProvider, IConfiguration configuration) : IJwtTokenIssuer
{
    private readonly JwtSecurityTokenHandler _handler = new();

    public IssuedToken IssueToken(string username, string role)
    {
        var issuer = configuration["Identity:Authority"]
            ?? throw new InvalidOperationException("Configuration value 'Identity:Authority' is required.");
        var audience = configuration["Identity:Audience"] ?? "airline-platform";
        var expirySeconds = configuration.GetValue("Identity:TokenExpirySeconds", 3600L);

        var now = DateTime.UtcNow;

        // ClaimTypes.Name/Role (not the short "sub"/"role" names) so User.Identity.Name and
        // User.IsInRole(...)/[Authorize(Roles = ...)] work against TokenValidationParameters'
        // default NameClaimType/RoleClaimType without needing to touch BuildingBlocks.Security's
        // validation wiring, which is already built and consumed as-is (per this slice's design note).
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, username),
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Role, role),
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: now,
            expires: now.AddSeconds(expirySeconds),
            signingCredentials: signingKeyProvider.SigningCredentials);

        return new IssuedToken(_handler.WriteToken(token), expirySeconds);
    }
}
