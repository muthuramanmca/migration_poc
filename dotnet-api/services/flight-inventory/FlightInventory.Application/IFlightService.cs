using FlightInventory.Application.Dtos;

namespace FlightInventory.Application;

/// <summary>
/// Backs all five flight endpoints, matching java-api's single FlightService. Note there is no
/// general update operation: route, fare, and departure time are immutable after creation, because
/// java-api exposes no endpoint to change them (spec section 2).
/// </summary>
public interface IFlightService
{
    Task<IReadOnlyList<FlightResponse>> ListAsync(CancellationToken cancellationToken = default);

    Task<FlightResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<FlightResponse> CreateAsync(FlightRequest request, CancellationToken cancellationToken = default);

    Task<FlightResponse> AdjustSeatsAsync(Guid id, SeatAdjustmentRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
