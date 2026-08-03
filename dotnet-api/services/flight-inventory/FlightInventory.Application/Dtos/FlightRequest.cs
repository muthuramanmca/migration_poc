namespace FlightInventory.Application.Dtos;

/// <summary>
/// Creation payload. Field names match java-api's FlightRequest exactly -- including
/// <c>DepartureAt</c> rather than the solution's usual <c>*Utc</c> suffix, which applies to the
/// entity property but not to the wire contract (design note section 2). Strangler Fig cutover
/// depends on both apps being able to serve the same clients.
///
/// <para><c>SeatCapacity</c> is nullable purely so a missing field is distinguishable from an
/// explicit 0. java-api used a primitive int, so an omitted capacity silently created a zero-seat
/// flight with a 201; <see cref="Validation.FlightRequestValidator"/> turns that into a 400
/// instead (spec rule 4.9, fixed per design note section 7.4).</para>
///
/// <para><c>Fare</c> needs no such treatment: 0 is not a legal fare, so the DecimalMin-equivalent
/// rule rejects an omitted fare on its own. In java-api the same omission passed validation
/// entirely -- Bean Validation treats null as valid -- and died at the NOT NULL constraint as an
/// unhandled 500.</para>
/// </summary>
public sealed record FlightRequest(
    string FlightNumber,
    string Origin,
    string Destination,
    DateTimeOffset DepartureAt,
    decimal Fare,
    int? SeatCapacity);
