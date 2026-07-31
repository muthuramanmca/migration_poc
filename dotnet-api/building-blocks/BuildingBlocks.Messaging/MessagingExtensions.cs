using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Messaging;

/// <summary>
/// Wires MassTransit + RabbitMQ for every service. The transactional outbox overload is for
/// services that publish an event as part of the same unit of work as a DB write (Booking,
/// FlightInventory); the plain overload is for pure consumers with no outbound side effect
/// of their own (Notification).
/// </summary>
public static class MessagingExtensions
{
    private const string DefaultRabbitMqConnectionString = "amqp://guest:guest@localhost:5672";

    /// <summary>Registers MassTransit with a transactional EF Core outbox backed by <typeparamref name="TDbContext"/>.</summary>
    public static IServiceCollection AddBuildingBlocksMessaging<TDbContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator> configureConsumersAndSagas)
        where TDbContext : DbContext
    {
        services.AddMassTransit(x =>
        {
            x.AddEntityFrameworkOutbox<TDbContext>(o =>
            {
                o.UseSqlServer();
                o.UseBusOutbox();
            });

            configureConsumersAndSagas(x);

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(configuration.GetConnectionString("RabbitMq") ?? DefaultRabbitMqConnectionString);
                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }

    /// <summary>Registers MassTransit without an outbox, for services that only consume (e.g. Notification).</summary>
    public static IServiceCollection AddBuildingBlocksMessaging(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator> configureConsumers)
    {
        services.AddMassTransit(x =>
        {
            configureConsumers(x);

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(configuration.GetConnectionString("RabbitMq") ?? DefaultRabbitMqConnectionString);
                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
