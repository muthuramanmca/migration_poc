using System.ComponentModel.DataAnnotations;
using MassTransit;

namespace Booking.Infrastructure.Sagas;

/// <summary>Persisted saga state for BookingCreationSaga -- lives in Booking.Db, not a shared store.</summary>
public class BookingSagaState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = string.Empty;
    public Guid PassengerId { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = default!;
}
