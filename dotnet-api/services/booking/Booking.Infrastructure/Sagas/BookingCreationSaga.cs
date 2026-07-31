using BuildingBlocks.Contracts;
using MassTransit;

namespace Booking.Infrastructure.Sagas;

/// <summary>
/// Orchestration-based saga for booking creation (see the migration plan's rationale: too few
/// participants for choreography to pay off, and centralizing state matches the entity-owned
/// state-machine convention already used elsewhere in this codebase).
///
/// Skeleton scope: the states, events, and transitions are real and wired end to end; what's
/// stubbed is FlightInventory's HoldSeatConsumer (always replies SeatHeld, no real availability
/// check) and the fact that nothing yet publishes BookingRequested in production, since there's
/// no booking-creation HTTP endpoint in this skeleton -- that lands with Booking's business-logic
/// pass. BookingCreationSagaTests exercises this saga directly via MassTransit's test harness.
///
/// ReleaseSeat (the compensating action) has a consumer on the FlightInventory side but no trigger
/// here yet: today SeatHeld transitions straight to Confirmed with no further step that could fail
/// after the hold succeeds. The real trigger point arrives once a step that can fail post-hold
/// exists (e.g. a Booking confirmation write, or a future Payment step) -- wiring a synthetic
/// failure path now would be fictitious, so it's deliberately left for that pass.
/// </summary>
public class BookingCreationSaga : MassTransitStateMachine<BookingSagaState>
{
    public State AwaitingSeatHold { get; private set; } = null!;
    public State Confirmed { get; private set; } = null!;
    public State Rejected { get; private set; } = null!;

    public Event<BookingRequested> BookingRequested { get; private set; } = null!;
    public Event<SeatHeld> SeatHeld { get; private set; } = null!;
    public Event<SeatHoldFailed> SeatHoldFailed { get; private set; } = null!;

    public BookingCreationSaga()
    {
        InstanceState(x => x.CurrentState);

        Event(() => BookingRequested, x => x.CorrelateById(m => m.Message.BookingId));
        Event(() => SeatHeld, x => x.CorrelateById(m => m.Message.BookingId));
        Event(() => SeatHoldFailed, x => x.CorrelateById(m => m.Message.BookingId));

        Initially(
            When(BookingRequested)
                .Then(context => context.Saga.PassengerId = context.Message.PassengerId)
                .Publish(context => new HoldSeat(context.Message.BookingId, context.Message.Items))
                .TransitionTo(AwaitingSeatHold));

        During(AwaitingSeatHold,
            When(SeatHeld)
                .Publish(context => new BookingConfirmed(context.Saga.CorrelationId, context.Saga.PassengerId, DateTimeOffset.UtcNow))
                .TransitionTo(Confirmed)
                .Finalize(),
            When(SeatHoldFailed)
                .Publish(context => new BookingCreationFailed(context.Saga.CorrelationId, context.Message.Reason))
                .TransitionTo(Rejected)
                .Finalize());

        SetCompletedWhenFinalized();
    }
}
