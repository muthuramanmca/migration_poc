using BuildingBlocks.Contracts;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace FlightInventory.Infrastructure.Consumers;

/// <summary>
/// Skeleton stub: always replies SeatHeld, no real availability check or decrement against
/// FlightInventoryDbContext. Proves the saga's request/reply wiring end to end; real seat-hold
/// logic (and the SeatHoldFailed path) lands with FlightInventory's business-logic pass.
/// </summary>
public class HoldSeatConsumer(ILogger<HoldSeatConsumer> logger) : IConsumer<HoldSeat>
{
    public async Task Consume(ConsumeContext<HoldSeat> context)
    {
        logger.LogInformation("Stub: acknowledging seat hold for booking {BookingId} (no real inventory check yet)", context.Message.BookingId);
        await context.Publish(new SeatHeld(context.Message.BookingId));
    }
}
