using FlightInventory.Application;
using FlightInventory.Domain;
using Microsoft.EntityFrameworkCore;

namespace FlightInventory.Infrastructure;

public class FlightRepository(FlightInventoryDbContext dbContext) : IFlightRepository
{
    public Task<Flight?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Flights.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

    public async Task AddAsync(Flight flight, CancellationToken cancellationToken = default)
    {
        dbContext.Flights.Add(flight);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
