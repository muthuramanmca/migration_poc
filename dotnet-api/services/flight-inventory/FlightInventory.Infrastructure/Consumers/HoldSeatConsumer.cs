using BuildingBlocks.Contracts;
using FlightInventory.Application;
using FlightInventory.Domain;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace FlightInventory.Infrastructure.Consumers;

/// <summary>
/// The saga's seat reservation. This is what java-api did as a synchronous in-process call --
/// BookingService called FlightService.findOrThrow and decremented seats inside Booking's own
/// transaction. Here it is a message, so atomicity with the booking write is the saga's job, not a
/// database transaction's.
///
/// <para>Two behaviours worth knowing about:</para>
/// <list type="bullet">
/// <item><b>Validate fully, then mutate.</b> Nothing is decremented until every line item is known
/// to be satisfiable, so the failure path leaves no partial reservation behind. java-api got the
/// same guarantee for free by rolling back its transaction; there is no transaction to roll back
/// here, and MassTransit's outbox filter saves the DbContext at the end of a successful consume --
/// so a half-applied decrement would be committed rather than discarded.</item>
/// <item><b>Idempotent.</b> A redelivered HoldSeat for a booking that already holds seats is
/// acknowledged without decrementing again. Redelivery is normal in messaging, and java-api had no
/// equivalent hazard because the call was in-process and happened exactly once.</item>
/// </list>
///
/// <para><c>BookingLineItem.FareClass</c> is ignored: java-api's Flight has a single Fare and no
/// fare classes, and no validated spec defines what a fare class means yet (design note section
/// 6.3.3). Booking's own slice picks this up.</para>
/// </summary>
public class HoldSeatConsumer(IFlightRepository flightRepository, ILogger<HoldSeatConsumer> logger) : IConsumer<HoldSeat>
{
    public async Task Consume(ConsumeContext<HoldSeat> context)
    {
        var bookingId = context.Message.BookingId;

        if (await flightRepository.HasHoldsForBookingAsync(bookingId, context.CancellationToken))
        {
            logger.LogInformation("Seats already held for booking {BookingId}; acknowledging without re-holding", bookingId);
            await context.Publish(new SeatHeld(bookingId));
            return;
        }

        // Collapsing repeated flight ids keeps the accept/reject decision identical to java-api's
        // per-item sequential check, which would have failed once the running capacity ran out.
        var requestedSeatsByFlight = context.Message.Items
            .GroupBy(item => item.FlightId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.SeatCount));

        var flights = new List<(Flight Flight, int SeatCount)>(requestedSeatsByFlight.Count);

        foreach (var (flightId, seatCount) in requestedSeatsByFlight)
        {
            var flight = await flightRepository.GetActiveByIdForUpdateAsync(flightId, context.CancellationToken);

            // Deactivated schedules are excluded by the lookup, so a cancelled flight can't be
            // booked -- java-api's rule 4.6, expressed as a failed hold instead of a 404 because
            // there is no HTTP response to return over a message bus.
            if (flight is null)
            {
                await FailAsync(context, bookingId, $"Flight not found or no longer scheduled: {flightId}");
                return;
            }

            if (flight.SeatCapacity < seatCount)
            {
                await FailAsync(
                    context,
                    bookingId,
                    $"Not enough seats for flight {flight.FlightNumber} (requested {seatCount}, available {flight.SeatCapacity})");
                return;
            }

            flights.Add((flight, seatCount));
        }

        var heldAtUtc = DateTimeOffset.UtcNow;

        foreach (var (flight, seatCount) in flights)
        {
            // Guarded above; TryAdjustSeats re-checks rather than trusting that.
            if (!flight.TryAdjustSeats(-seatCount))
            {
                await FailAsync(context, bookingId, $"Not enough seats for flight {flight.FlightNumber}");
                return;
            }
        }

        flightRepository.AddHolds(flights.Select(entry => new SeatHold
        {
            Id = Guid.NewGuid(),
            BookingId = bookingId,
            FlightId = entry.Flight.Id,
            SeatCount = entry.SeatCount,
            HeldAtUtc = heldAtUtc,
        }));

        // One save for every line item, matching java-api's single-transaction, no-intermediate-save
        // shape. A concurrency conflict propagates deliberately so MassTransit retries -- treating a
        // transient conflict as a permanent booking failure would be wrong (see FlightRepository).
        await flightRepository.SaveChangesAsync(context.CancellationToken);

        logger.LogInformation("Held seats on {FlightCount} flight(s) for booking {BookingId}", flights.Count, bookingId);

        await context.Publish(new SeatHeld(bookingId));
    }

    private async Task FailAsync(ConsumeContext<HoldSeat> context, Guid bookingId, string reason)
    {
        // No seats have been decremented at this point, so there is nothing to compensate for.
        logger.LogInformation("Seat hold failed for booking {BookingId}: {Reason}", bookingId, reason);
        await context.Publish(new SeatHoldFailed(bookingId, reason));
    }
}
