using Booking.Application;
using Booking.Infrastructure.Sagas;
using BuildingBlocks.Messaging;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Booking.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBookingInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<BookingDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("BookingDb")));

        services.AddScoped<IBookingRepository, BookingRepository>();

        services.AddBuildingBlocksMessaging<BookingDbContext>(configuration, x =>
        {
            x.AddSagaStateMachine<BookingCreationSaga, BookingSagaState>()
                .EntityFrameworkRepository(r =>
                {
                    r.ExistingDbContext<BookingDbContext>();
                    r.UseSqlServer();
                });
        });

        return services;
    }
}
