namespace FlightInventory.Application.Dtos;

/// <summary>
/// Read model for every flight endpoint. <c>Active</c> is deliberately absent, matching java-api:
/// clients never see the soft-delete flag because deactivated flights are simply invisible
/// (spec rule 4.6). <c>LowSeatAvailability</c> is computed per response, never stored.
/// </summary>
public sealed record FlightResponse(
    Guid Id,
    string FlightNumber,
    string Origin,
    string Destination,
    DateTimeOffset DepartureAt,
    decimal Fare,
    int SeatCapacity,
    bool LowSeatAvailability);
