using FlightInventory.Domain;

namespace FlightInventory.Application;

/// <summary>
/// <para><b>The tracked/untracked split is deliberate and load-bearing.</b> java-api's
/// FlightService.findOrThrow returned a JPA-managed entity when Booking called it inside a
/// transaction, but a detached one when the read endpoints called it -- same method, two
/// persistence semantics decided by the caller (spec section 6). EF Core has no equivalent ambient
/// behaviour, so the distinction is in the method names instead: read paths get an AsNoTracking
/// instance, write paths get a tracked one. Calling the read variant from a write path is the
/// silent-data-loss bug this shape exists to prevent.</para>
///
/// <para>Likewise <see cref="SaveChangesAsync"/> is explicit. java-api never calls save() on the
/// adjust or delete paths at all -- JPA dirty checking persists them at commit. Nothing does that
/// here.</para>
/// </summary>
public interface IFlightRepository
{
    Task<IReadOnlyList<Flight>> ListActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>Read-only lookup; excludes deactivated flights, so a cancelled schedule is a 404 (spec rule 4.6).</summary>
    Task<Flight?> GetActiveByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Tracked lookup for mutation paths. Must be paired with <see cref="SaveChangesAsync"/>.</summary>
    Task<Flight?> GetActiveByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Scoped to active flights, so a flight number frees up once its schedule is cancelled.
    /// java-api's check spanned soft-deleted rows too, permanently burning any number that had ever
    /// been used (spec rule 4.8, fixed per design note section 7.3).
    /// </summary>
    Task<bool> ExistsByFlightNumberAsync(string flightNumber, CancellationToken cancellationToken = default);

    Task AddAsync(Flight flight, CancellationToken cancellationToken = default);

    // --- Saga seat holds (see SeatHold) -----------------------------------------------------

    Task<bool> HasHoldsForBookingAsync(Guid bookingId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SeatHold>> GetHoldsForBookingAsync(Guid bookingId, CancellationToken cancellationToken = default);

    void AddHolds(IEnumerable<SeatHold> holds);

    void RemoveHolds(IEnumerable<SeatHold> holds);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
