namespace BuildingBlocks.Contracts;

/// <summary>
/// Integration events/commands for the Booking-creation saga. These are the only public contract
/// between Booking, FlightInventory, and Notification -- no service references another's Domain/Infrastructure.
/// One line item per seat/fare being requested.
/// </summary>
public sealed record BookingLineItem(Guid FlightId, int SeatCount, string FareClass);

/// <summary>Published by Booking's outbox after a Booking row commits locally (status Pending).</summary>
public sealed record BookingRequested(Guid BookingId, Guid PassengerId, IReadOnlyList<BookingLineItem> Items, DateTimeOffset RequestedAtUtc);

/// <summary>Command: saga asks FlightInventory to place a temporary hold on the requested seats.</summary>
public sealed record HoldSeat(Guid BookingId, IReadOnlyList<BookingLineItem> Items);

/// <summary>Reply: FlightInventory successfully held every line item.</summary>
public sealed record SeatHeld(Guid BookingId);

/// <summary>Reply: FlightInventory could not hold one or more line items.</summary>
public sealed record SeatHoldFailed(Guid BookingId, string Reason);

/// <summary>Compensating command: saga asks FlightInventory to release a previously-held hold.</summary>
public sealed record ReleaseSeat(Guid BookingId);

/// <summary>Published by the saga once the booking is fully confirmed. Notification consumes this.</summary>
public sealed record BookingConfirmed(Guid BookingId, Guid PassengerId, DateTimeOffset ConfirmedAtUtc);

/// <summary>Published by the saga if the booking could not be created.</summary>
public sealed record BookingCreationFailed(Guid BookingId, string Reason);
