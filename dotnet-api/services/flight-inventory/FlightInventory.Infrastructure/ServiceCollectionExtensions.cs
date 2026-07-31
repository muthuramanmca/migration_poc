using BuildingBlocks.Messaging;
using FlightInventory.Application;
using FlightInventory.Infrastructure.Consumers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FlightInventory.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFlightInventoryInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<FlightInventoryDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("FlightInventoryDb")));

        services.AddScoped<IFlightRepository, FlightRepository>();

        services.AddBuildingBlocksMessaging<FlightInventoryDbContext>(configuration, x =>
        {
            x.AddConsumer<HoldSeatConsumer>();
            x.AddConsumer<ReleaseSeatConsumer>();
        });

        return services;
    }
}
