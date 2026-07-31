using FlightInventory.Domain;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace FlightInventory.Infrastructure;

/// <summary>
/// Outbox entities (Inbox/Outbox state + messages) are added here so publishing SeatHeld/SeatHoldFailed
/// happens atomically with the local write that triggers it -- see BuildingBlocks.Messaging's
/// AddEntityFrameworkOutbox&lt;FlightInventoryDbContext&gt; registration.
/// </summary>
public class FlightInventoryDbContext(DbContextOptions<FlightInventoryDbContext> options) : DbContext(options)
{
    public DbSet<Flight> Flights => Set<Flight>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Flight>(e =>
        {
            e.HasKey(f => f.Id);
            e.Property(f => f.FlightNumber).IsRequired().HasMaxLength(16);
        });

        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
