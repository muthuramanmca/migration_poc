using Booking.Infrastructure.Sagas;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using BookingEntity = Booking.Domain.Booking;

namespace Booking.Infrastructure;

/// <summary>
/// Outbox entities let BookingRequested publish atomically with the local Booking write (see
/// BuildingBlocks.Messaging's AddEntityFrameworkOutbox&lt;BookingDbContext&gt; registration).
/// BookingSagaStates is the saga's own persisted state, living in this same DB (database-per-service).
/// </summary>
public class BookingDbContext(DbContextOptions<BookingDbContext> options) : DbContext(options)
{
    public DbSet<BookingEntity> Bookings => Set<BookingEntity>();
    public DbSet<BookingSagaState> BookingSagaStates => Set<BookingSagaState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BookingEntity>(e =>
        {
            e.HasKey(b => b.Id);
        });

        modelBuilder.Entity<BookingSagaState>(e =>
        {
            e.HasKey(s => s.CorrelationId);
            e.Property(s => s.CurrentState).HasMaxLength(64);
        });

        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
