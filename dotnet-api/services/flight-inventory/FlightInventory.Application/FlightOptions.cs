namespace FlightInventory.Application;

/// <summary>
/// Bound from the "FlightInventory" configuration section. The .NET replacement for java-api's
/// <c>@Value("${app.flights.low-seat-threshold}")</c> field injection -- and, unlike that, testable
/// without reflection, which is exactly why java-api's own test suite could never have caught the
/// list-vs-get threshold divergence (spec section 7).
/// </summary>
public sealed class FlightOptions
{
    public const string SectionName = "FlightInventory";

    /// <summary>A flight is flagged low-availability when SeatCapacity is strictly below this.</summary>
    public int LowSeatThreshold { get; set; } = 10;
}
