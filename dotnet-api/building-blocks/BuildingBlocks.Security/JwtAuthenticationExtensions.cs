using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace BuildingBlocks.Security;

/// <summary>
/// Shared JWT-bearer wiring for every service (Gateway included -- zero trust, every hop validates
/// independently rather than trusting the network boundary alone). Validates RS256-signed tokens
/// against Identity's JWKS, discovered from its OpenID Connect metadata document.
///
/// Skeleton note: this is validation wiring only. It has nothing to validate against until Identity's
/// real token-issuance implementation (deferred to that slice's 02-04 pass) actually serves
/// /.well-known/openid-configuration + a JWKS document -- until then every request 401s, which is
/// the expected/correct behavior for a skeleton with no real auth server behind it yet.
/// </summary>
public static class JwtAuthenticationExtensions
{
    public static IServiceCollection AddBuildingBlocksJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var identityAuthority = configuration["Identity:Authority"]
            ?? throw new InvalidOperationException("Configuration value 'Identity:Authority' is required.");

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = identityAuthority;
                options.RequireHttpsMetadata = !configuration.GetValue("Identity:AllowHttpMetadata", false);
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = identityAuthority,
                    ValidateAudience = true,
                    ValidAudience = configuration["Identity:Audience"] ?? "airline-platform",
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });

        // JwtTokenIssuer mints the role as a ClaimTypes.Role claim, which is also
        // TokenValidationParameters' default RoleClaimType -- so RequireRole works here with no
        // extra claim mapping. See BuildingBlocks.Security.AuthorizationPolicies.
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.AdminOnly, policy => policy.RequireRole(Roles.Admin));
        });

        return services;
    }
}
