namespace FlightInventory.Application.Dtos;

/// <summary>
/// Signed seat delta: positive adds, negative removes.
///
/// <para>Nullable, and validated. java-api's version was a bare primitive int on a body the
/// controller never marked @Valid, so <c>PUT /api/flights/{id}/seats</c> with an empty body
/// returned 200 with the flight unchanged -- a no-op that looks like success on an admin inventory
/// mutation (spec rule 4.5, fixed per design note section 7.2). Here a missing Delta is a 400.</para>
/// </summary>
public sealed record SeatAdjustmentRequest(int? Delta);
