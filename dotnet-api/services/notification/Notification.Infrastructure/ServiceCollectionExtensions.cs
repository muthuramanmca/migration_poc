using BuildingBlocks.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Notification.Application;
using Notification.Infrastructure.Consumers;

namespace Notification.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNotificationInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<NotificationDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("NotificationDb")));

        services.AddScoped<INotificationRecordRepository, NotificationRecordRepository>();

        // No outbox overload -- Notification only consumes, it never publishes as part of its own DB write.
        services.AddBuildingBlocksMessaging(configuration, x =>
        {
            x.AddConsumer<BookingConfirmedConsumer>();
        });

        return services;
    }
}
