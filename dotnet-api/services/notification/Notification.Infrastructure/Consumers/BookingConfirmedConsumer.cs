using BuildingBlocks.Contracts;
using MassTransit;
using Microsoft.Extensions.Logging;
using Notification.Application;
using Notification.Domain;

namespace Notification.Infrastructure.Consumers;

/// <summary>
/// Skeleton stub: logs + records that a confirmation would be sent, no real email/SMS provider
/// call. Demonstrates genuine event-driven decoupling -- neither Booking nor the saga know this
/// consumer exists. Real delivery logic lands with Notification's business-logic pass.
/// </summary>
public class BookingConfirmedConsumer(INotificationRecordRepository repository, ILogger<BookingConfirmedConsumer> logger) : IConsumer<BookingConfirmed>
{
    public async Task Consume(ConsumeContext<BookingConfirmed> context)
    {
        logger.LogInformation("Stub: would send booking confirmation for {BookingId} to passenger {PassengerId}", context.Message.BookingId, context.Message.PassengerId);

        await repository.AddAsync(new NotificationRecord
        {
            Id = Guid.NewGuid(),
            BookingId = context.Message.BookingId,
            Type = "BookingConfirmed",
            SentAtUtc = DateTimeOffset.UtcNow,
        }, context.CancellationToken);
    }
}
