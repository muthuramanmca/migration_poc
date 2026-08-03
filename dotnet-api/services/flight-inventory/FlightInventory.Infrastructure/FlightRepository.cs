using BuildingBlocks.Common;
using FlightInventory.Application;
using FlightInventory.Domain;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FlightInventory.Infrastructure;

public class FlightRepository(FlightInventoryDbContext dbContext) : IFlightRepository
{
    public async Task<IReadOnlyList<Flight>> ListActiveAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Flights
            .AsNoTracking()
            .Where(f => f.Active)
            .ToListAsync(cancellationToken);

    public Task<Flight?> GetActiveByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Flights
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id && f.Active, cancellationToken);

    public Task<Flight?> GetActiveByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Flights
            .FirstOrDefaultAsync(f => f.Id == id && f.Active, cancellationToken);

    public Task<bool> ExistsByFlightNumberAsync(string flightNumber, CancellationToken cancellationToken = default) =>
        dbContext.Flights
            .AsNoTracking()
            .AnyAsync(f => f.FlightNumber == flightNumber && f.Active, cancellationToken);

    public async Task AddAsync(Flight flight, CancellationToken cancellationToken = default)
    {
        dbContext.Flights.Add(flight);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // Closes the creation race the spec flags as unhandled in java-api: FlightService's
            // Exists check and this insert aren't atomic, so two concurrent creates of the same
            // flight number can both pass the check and collide here. Same treatment Identity's
            // UserRepository already applies to duplicate registrations, rather than letting this
            // surface as an unhandled 500.
            throw ApiException.Conflict("DUPLICATE_FLIGHT_NUMBER", "A flight with this number already exists");
        }
    }

    public Task<bool> HasHoldsForBookingAsync(Guid bookingId, CancellationToken cancellationToken = default) =>
        dbContext.SeatHolds
            .AsNoTracking()
            .AnyAsync(h => h.BookingId == bookingId, cancellationToken);

    public async Task<IReadOnlyList<SeatHold>> GetHoldsForBookingAsync(Guid bookingId, CancellationToken cancellationToken = default) =>
        await dbContext.SeatHolds
            .Where(h => h.BookingId == bookingId)
            .ToListAsync(cancellationToken);

    public void AddHolds(IEnumerable<SeatHold> holds) => dbContext.SeatHolds.AddRange(holds);

    public void RemoveHolds(IEnumerable<SeatHold> holds) => dbContext.SeatHolds.RemoveRange(holds);

    /// <summary>
    /// Deliberately does not translate <see cref="DbUpdateConcurrencyException"/>. A concurrency
    /// conflict means different things to the two callers: FlightService turns it into a 409, while
    /// the saga consumers let it propagate so MassTransit can retry -- turning a transient conflict
    /// into a permanent booking failure would be wrong.
    /// </summary>
    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);

    private static bool IsUniqueConstraintViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };
}
