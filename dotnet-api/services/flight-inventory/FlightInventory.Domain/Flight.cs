namespace FlightInventory.Domain;

/// <summary>
/// A scheduled flight and its seat inventory.
///
/// <para><b>SeatCapacity is a live remaining-seats counter, not a static aircraft configuration</b>
/// (spec rule 4.7). The saga's HoldSeat decrements it and ReleaseSeat increments it, exactly as
/// java-api's BookingService did in-process. There is deliberately no separate seats-sold count in
/// this pass -- that number is only knowable from Booking, whose spec isn't written yet (design
/// note section 7.5). The name is kept identical to java-api's field so 04_04's compare stays
/// honest.</para>
///
/// <para>Seat and availability rules live on the entity rather than in FlightService, matching
/// java-api's Flight -- spec rule 4.2 specifically flags entity-resident logic as the thing a
/// service-only reading of the source would miss.</para>
/// </summary>
public class Flight
{
    public Guid Id { get; set; }
    public string FlightNumber { get; set; } = string.Empty;
    public string Origin { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public DateTimeOffset DepartureAtUtc { get; set; }
    public decimal Fare { get; set; }
    public int SeatCapacity { get; set; }

    /// <summary>
    /// Soft-delete flag (spec rule 4.6). A cancelled schedule is deactivated, never removed, so
    /// fare snapshots on already-booked itineraries stay intact. Every read filters on this, so a
    /// deactivated flight is invisible -- and it is never returned to clients.
    /// </summary>
    public bool Active { get; set; } = true;

    /// <summary>
    /// Optimistic-concurrency token. java-api had no equivalent and could silently lose one of two
    /// interleaved seat updates; here the risk is strictly worse, because admin seat adjustments
    /// and the saga's seat holds run in <i>different processes</i> with no shared transaction
    /// (design note section 7.6).
    /// </summary>
    public byte[]? RowVersion { get; set; }

    /// <summary>
    /// Threshold comes from configuration (FlightInventory:LowSeatThreshold) and is passed in per
    /// call, so there is exactly one source for it. java-api pushed the threshold onto each entity
    /// instance instead, and its list endpoint skipped that step -- leaving the list and single-get
    /// endpoints able to disagree about the same flight (spec rule 4.3, fixed per design note
    /// section 7.1). Comparison is strictly less-than: capacity == threshold is not low.
    /// </summary>
    public bool IsLowSeatAvailability(int lowSeatThreshold) => SeatCapacity < lowSeatThreshold;

    /// <summary>
    /// Applies a signed seat delta, rejecting only an adjustment that would drive capacity below
    /// zero (spec rule 4.4). Zero is allowed; the guard is evaluated before any mutation, so a
    /// rejected adjustment leaves the entity untouched -- there is no partial state to roll back.
    /// Returns false instead of throwing so the two callers can shape their own failure: a 409 for
    /// the admin endpoint, a SeatHoldFailed reply for the saga.
    /// </summary>
    public bool TryAdjustSeats(int delta)
    {
        var resultingSeats = SeatCapacity + delta;
        if (resultingSeats < 0)
        {
            return false;
        }

        SeatCapacity = resultingSeats;
        return true;
    }

    public void Deactivate() => Active = false;
}
