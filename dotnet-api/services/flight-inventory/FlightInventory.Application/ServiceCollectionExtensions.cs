using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FlightInventory.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFlightInventoryApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<FlightOptions>(configuration.GetSection(FlightOptions.SectionName));

        services.AddScoped<IFlightService, FlightService>();
        services.AddValidatorsFromAssemblyContaining<FlightService>();

        return services;
    }
}
