using Microsoft.EntityFrameworkCore;
using Notification.Domain;

namespace Notification.Infrastructure;

/// <summary>No outbox here -- Notification only consumes events; it doesn't publish anything that needs transactional delivery guarantees of its own.</summary>
public class NotificationDbContext(DbContextOptions<NotificationDbContext> options) : DbContext(options)
{
    public DbSet<NotificationRecord> NotificationRecords => Set<NotificationRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NotificationRecord>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.Type).IsRequired().HasMaxLength(32);
        });
    }
}
