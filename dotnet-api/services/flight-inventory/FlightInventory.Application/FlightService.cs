using BuildingBlocks.Common;
using FlightInventory.Application.Dtos;
using FlightInventory.Domain;
using Microsoft.Extensions.Options;

namespace FlightInventory.Application;

public sealed class FlightService(IFlightRepository flightRepository, IOptions<FlightOptions> options) : IFlightService
{
    private int LowSeatThreshold => options.Value.LowSeatThreshold;

    public async Task<IReadOnlyList<FlightResponse>> ListAsync(CancellationToken cancellationToken = default)
    {
        // Returns every active flight, unpaginated and unsorted -- faithful to java-api. Paging and
        // origin/destination/date filtering were deliberately deferred so this endpoint stays
        // wire-identical during Strangler Fig cutover (design note section 7.7).
        var flights = await flightRepository.ListActiveAsync(cancellationToken);

        // Same threshold, same mapping as GetByIdAsync below. java-api's list endpoint bypassed the
        // configured value and fell back to a hardcoded 10, so the two endpoints could report
        // different availability for the same flight (spec rule 4.3).
        return flights.Select(ToResponse).ToList();
    }

    public async Task<FlightResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var flight = await flightRepository.GetActiveByIdAsync(id, cancellationToken)
            ?? throw NotFound(id);

        return ToResponse(flight);
    }

    public async Task<FlightResponse> CreateAsync(FlightRequest request, CancellationToken cancellationToken = default)
    {
        if (await flightRepository.ExistsByFlightNumberAsync(request.FlightNumber, cancellationToken))
        {
            throw ApiException.Conflict("DUPLICATE_FLIGHT_NUMBER", "A flight with this number already exists");
        }

        var flight = new Flight
        {
            Id = Guid.NewGuid(),
            FlightNumber = request.FlightNumber,
            Origin = request.Origin,
            Destination = request.Destination,
            DepartureAtUtc = request.DepartureAt,
            Fare = request.Fare,

            // Validator guarantees non-null; the fallback keeps this safe when the service is
            // exercised directly (04_03) rather than through the endpoint's validation filter.
            SeatCapacity = request.SeatCapacity ?? 0,
            Active = true,
        };

        // May itself throw ApiException.Conflict if a concurrent create wins the race between the
        // Exists check above and this insert -- see FlightRepository.AddAsync.
        await flightRepository.AddAsync(flight, cancellationToken);

        return ToResponse(flight);
    }

    public async Task<FlightResponse> AdjustSeatsAsync(Guid id, SeatAdjustmentRequest request, CancellationToken cancellationToken = default)
    {
        var delta = request.Delta
            ?? throw ApiException.BadRequest(
                "VALIDATION_FAILED",
                "Request validation failed",
                new Dictionary<string, object?> { ["fieldErrors"] = new[] { "Delta: 'Delta' must not be empty." } });

        var flight = await flightRepository.GetActiveByIdForUpdateAsync(id, cancellationToken)
            ?? throw NotFound(id);

        if (!flight.TryAdjustSeats(delta))
        {
            // Names the flight number rather than the id, matching java-api's message.
            throw ApiException.Conflict(
                "INSUFFICIENT_SEATS",
                $"Seat adjustment would result in a negative capacity for flight {flight.FlightNumber}");
        }

        await SaveTrackedChangesAsync(cancellationToken);

        return ToResponse(flight);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Soft delete: the row survives so fare snapshots on already-booked itineraries stay
        // intact. Not idempotent -- a second call 404s, exactly as java-api does (spec rule 4.6).
        var flight = await flightRepository.GetActiveByIdForUpdateAsync(id, cancellationToken)
            ?? throw NotFound(id);

        flight.Deactivate();

        await SaveTrackedChangesAsync(cancellationToken);
    }

    /// <summary>
    /// java-api relied on JPA dirty checking to persist adjust/delete at transaction commit and
    /// never called save() on either path. EF Core does nothing of the sort, so the call is
    /// explicit here -- and the concurrency conflict it can now surface has no java-api equivalent
    /// (design note section 7.6).
    /// </summary>
    private async Task SaveTrackedChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await flightRepository.SaveChangesAsync(cancellationToken);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
        {
            throw ApiException.Conflict(
                "CONCURRENT_MODIFICATION",
                "This flight was modified by another request. Re-read it and retry.");
        }
    }

    private static ApiException NotFound(Guid id) =>
        ApiException.NotFound("FLIGHT_NOT_FOUND", $"Flight not found: {id}");

    private FlightResponse ToResponse(Flight flight) => new(
        flight.Id,
        flight.FlightNumber,
        flight.Origin,
        flight.Destination,
        flight.DepartureAtUtc,
        flight.Fare,
        flight.SeatCapacity,
        flight.IsLowSeatAvailability(LowSeatThreshold));
}
