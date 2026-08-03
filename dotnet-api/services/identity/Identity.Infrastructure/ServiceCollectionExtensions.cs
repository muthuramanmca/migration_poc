using BuildingBlocks.Messaging;
using Identity.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<IdentityDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("IdentityDb")));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IMfaChallengeProvider, NoOpMfaChallengeProvider>();
        services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
        services.AddScoped<IJwtTokenIssuer, JwtTokenIssuer>();
        services.AddSingleton<RsaSigningKeyProvider>();

        // Producer only -- Identity has no consumers/sagas of its own, just PassengerRegisteredEvent
        // published via the outbox registered below (see IdentityDbContext).
        services.AddBuildingBlocksMessaging<IdentityDbContext>(configuration, _ => { });

        return services;
    }
}
