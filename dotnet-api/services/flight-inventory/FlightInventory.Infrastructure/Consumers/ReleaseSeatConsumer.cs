using BuildingBlocks.Contracts;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace FlightInventory.Infrastructure.Consumers;

/// <summary>
/// Skeleton stub for the saga's compensating action: acknowledges only, no real seat-count
/// increment yet. Real release logic lands with FlightInventory's business-logic pass.
/// </summary>
public class ReleaseSeatConsumer(ILogger<ReleaseSeatConsumer> logger) : IConsumer<ReleaseSeat>
{
    public Task Consume(ConsumeContext<ReleaseSeat> context)
    {
        logger.LogInformation("Stub: acknowledging seat release for booking {BookingId} (no real inventory update yet)", context.Message.BookingId);
        return Task.CompletedTask;
    }
}
