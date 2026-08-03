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

    public DbSet<SeatHold> SeatHolds => Set<SeatHold>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Flight>(e =>
        {
            e.HasKey(f => f.Id);
            e.Property(f => f.FlightNumber).IsRequired().HasMaxLength(16);
            e.Property(f => f.Origin).IsRequired().HasMaxLength(64);
            e.Property(f => f.Destination).IsRequired().HasMaxLength(64);
            e.Property(f => f.Fare).HasPrecision(18, 2);
            e.Property(f => f.Active).IsRequired();
            e.Property(f => f.RowVersion).IsRowVersion();

            // Filtered so a flight number frees up when its schedule is cancelled. java-api's
            // uniqueness check spanned soft-deleted rows, permanently burning any number that had
            // ever been used -- and real airlines reuse numbers every season (spec rule 4.8).
            e.HasIndex(f => f.FlightNumber).IsUnique().HasFilter("[Active] = 1");

            // Every read filters on Active (spec rule 4.6), so it leads the index.
            e.HasIndex(f => new { f.Active, f.DepartureAtUtc });
        });

        modelBuilder.Entity<SeatHold>(e =>
        {
            e.HasKey(h => h.Id);
            e.HasIndex(h => h.BookingId);

            // One hold row per booking/flight pair -- the DB-level guard behind HoldSeat's
            // idempotency check, for the case where two copies of the same message are handled
            // concurrently rather than sequentially.
            e.HasIndex(h => new { h.BookingId, h.FlightId }).IsUnique();
        });

        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
