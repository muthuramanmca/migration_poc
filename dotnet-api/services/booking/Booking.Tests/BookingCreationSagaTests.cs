using Booking.Infrastructure.Sagas;
using BuildingBlocks.Contracts;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Booking.Tests;

/// <summary>
/// Proves the saga's states/events/transitions actually work end to end, using MassTransit's
/// in-memory test harness -- no real broker or DB needed. This is the skeleton's substitute for a
/// live "publish a test BookingRequested" endpoint: it exercises the same wiring without adding
/// real API surface ahead of Booking's business-logic pass.
/// </summary>
public class BookingCreationSagaTests
{
    [Fact]
    public async Task HappyPath_HoldsSeat_ThenConfirmsBooking()
    {
        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(x =>
            {
                x.AddSagaStateMachine<BookingCreationSaga, BookingSagaState>()
                    .InMemoryRepository();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var bookingId = Guid.NewGuid();
        var passengerId = Guid.NewGuid();

        await harness.Bus.Publish(new BookingRequested(
            bookingId,
            passengerId,
            [new BookingLineItem(Guid.NewGuid(), 1, "Economy")],
            DateTimeOffset.UtcNow));

        Assert.True(await harness.Published.Any<HoldSeat>());

        var sagaHarness = harness.GetSagaStateMachineHarness<BookingCreationSaga, BookingSagaState>();
        Assert.True(await sagaHarness.Consumed.Any<BookingRequested>());

        var sagaState = sagaHarness.Created.Contains(bookingId);
        Assert.NotNull(sagaState);
        Assert.Equal(sagaHarness.StateMachine.AwaitingSeatHold.Name, sagaState.CurrentState);

        await harness.Bus.Publish(new SeatHeld(bookingId));

        Assert.True(await harness.Published.Any<BookingConfirmed>());
    }

    [Fact]
    public async Task SeatHoldFailedPath_RejectsBooking()
    {
        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(x =>
            {
                x.AddSagaStateMachine<BookingCreationSaga, BookingSagaState>()
                    .InMemoryRepository();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var bookingId = Guid.NewGuid();

        await harness.Bus.Publish(new BookingRequested(
            bookingId,
            Guid.NewGuid(),
            [new BookingLineItem(Guid.NewGuid(), 1, "Economy")],
            DateTimeOffset.UtcNow));

        await harness.Bus.Publish(new SeatHoldFailed(bookingId, "No seats available"));

        Assert.True(await harness.Published.Any<BookingCreationFailed>());
    }
}
