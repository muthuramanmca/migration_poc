using FlightInventory.Domain;

namespace FlightInventory.Application;

/// <summary>Signatures only -- implementation (FlightInventory.Infrastructure) and real seat-hold logic land with FlightInventory's business-logic pass.</summary>
public interface IFlightRepository
{
    Task<Flight?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Flight flight, CancellationToken cancellationToken = default);
}
