namespace FlightInventory.Domain;

/// <summary>
/// One held line item, recorded when the saga's HoldSeat succeeds so the compensating ReleaseSeat
/// knows what to give back.
///
/// <para><b>Why this exists:</b> the frozen saga contract's <c>ReleaseSeat(Guid BookingId)</c>
/// carries only a booking id -- no flight ids, no seat counts -- so FlightInventory cannot honour a
/// release unless it remembered the hold. java-api had no equivalent because Booking held the line
/// items in-process and passed them straight back. This table is the minimum needed to make the
/// existing contract functional, and it is FlightInventory's own data (no cross-service reads).</para>
///
/// <para>It also makes HoldSeat idempotent: a redelivered HoldSeat for a booking that already has
/// holds is acknowledged without decrementing seats a second time, which matters because message
/// redelivery is normal, not exceptional.</para>
///
/// <para>Booking's own slice may revisit this once its spec defines hold expiry and the post-hold
/// failure path that actually triggers ReleaseSeat (ADR 0001 notes nothing triggers it today).</para>
/// </summary>
public class SeatHold
{
    public Guid Id { get; set; }
    public Guid BookingId { get; set; }
    public Guid FlightId { get; set; }
    public int SeatCount { get; set; }
    public DateTimeOffset HeldAtUtc { get; set; }
}
