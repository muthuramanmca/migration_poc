namespace BuildingBlocks.Contracts;

/// <summary>
/// Published by Identity's outbox after a new Passenger commits locally. Consumed by Loyalty to
/// auto-provision a LoyaltyAccount -- Identity has no reference to Loyalty at all, the coupling
/// exists only through this contract and the message bus (mirrors java-api's
/// PassengerRegisteredEvent / LoyaltyEventListener relationship, but after-commit via the outbox
/// instead of synchronously inside the open transaction).
/// </summary>
public sealed record PassengerRegisteredEvent(Guid PassengerId, string Username, DateTimeOffset RegisteredAtUtc);
