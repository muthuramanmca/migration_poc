namespace FlightInventory.Domain;

/// <summary>
/// Placeholder entity -- shape only, enough for EF Core migrations to run. Real fields
/// (schedule, fare classes, seat map) and stock-management behavior land with FlightInventory's
/// business-logic pass (migration plan steps 02-04 for this slice).
/// </summary>
public class Flight
{
    public Guid Id { get; set; }
    public string FlightNumber { get; set; } = string.Empty;
    public DateTimeOffset DepartureAtUtc { get; set; }
    public int AvailableSeats { get; set; }
}
