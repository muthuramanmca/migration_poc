using BuildingBlocks.Contracts;
using FlightInventory.Application;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace FlightInventory.Infrastructure.Consumers;

/// <summary>
/// The saga's compensating action -- the messaging equivalent of java-api's cancel path, which
/// looked up each flight and called increaseSeats to give the seats back.
///
/// <para>ReleaseSeat carries only a booking id, so the seat counts come from the SeatHold rows
/// written when the hold succeeded. Deleting those rows is what makes a repeated release a no-op
/// rather than a double credit.</para>
///
/// <para><b>Note:</b> ADR 0001 records that nothing in the current saga triggers this yet --
/// SeatHeld transitions straight to Confirmed with no later step that can fail. It is implemented
/// here because the seats and the hold records are FlightInventory's own data; wiring the trigger
/// belongs to Booking's slice.</para>
/// </summary>
public class ReleaseSeatConsumer(IFlightRepository flightRepository, ILogger<ReleaseSeatConsumer> logger) : IConsumer<ReleaseSeat>
{
    public async Task Consume(ConsumeContext<ReleaseSeat> context)
    {
        var bookingId = context.Message.BookingId;

        var holds = await flightRepository.GetHoldsForBookingAsync(bookingId, context.CancellationToken);
        if (holds.Count == 0)
        {
            logger.LogInformation("No seat holds recorded for booking {BookingId}; nothing to release", bookingId);
            return;
        }

        foreach (var hold in holds)
        {
            var flight = await flightRepository.GetActiveByIdForUpdateAsync(hold.FlightId, context.CancellationToken);

            // java-api threw here, because its cancel path reused the same active-only lookup and a
            // cancelled schedule is invisible. Throwing would fault a compensating message, so the
            // hold record is cleared and the release is logged instead -- the seats belong to a
            // flight nobody can book anyway.
            if (flight is null)
            {
                logger.LogWarning(
                    "Flight {FlightId} is no longer scheduled; dropping the {SeatCount}-seat hold for booking {BookingId} without restoring capacity",
                    hold.FlightId, hold.SeatCount, bookingId);
                continue;
            }

            flight.TryAdjustSeats(hold.SeatCount);
        }

        flightRepository.RemoveHolds(holds);

        await flightRepository.SaveChangesAsync(context.CancellationToken);

        logger.LogInformation("Released {HoldCount} seat hold(s) for booking {BookingId}", holds.Count, bookingId);
    }
}
