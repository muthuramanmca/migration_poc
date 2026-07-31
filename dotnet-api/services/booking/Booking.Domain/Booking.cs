namespace Booking.Domain;

/// <summary>
/// Placeholder entity -- shape only, enough for EF Core migrations to run. Keeps the state
/// transition on the entity itself (matching java-api's Order.transitionTo() convention, per
/// CLAUDE.md), but without real invariant checking yet -- that lands with Booking's
/// business-logic pass (migration plan steps 02-04 for this slice).
/// </summary>
public class Booking
{
    public Guid Id { get; set; }
    public Guid PassengerId { get; set; }
    public BookingStatus Status { get; private set; } = BookingStatus.Pending;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public void TransitionTo(BookingStatus status)
    {
        Status = status;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}
