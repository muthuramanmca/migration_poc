using Identity.Domain;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure;

/// <summary>
/// Outbox entities let PassengerRegisteredEvent publish atomically with the local User write --
/// see BuildingBlocks.Messaging's AddEntityFrameworkOutbox&lt;IdentityDbContext&gt; registration
/// (ServiceCollectionExtensions). This is the direct fix for the "fires inside the open
/// transaction, before commit" antipattern the behavior spec flags in rule 4.8.
/// </summary>
public class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.Property(u => u.Username).IsRequired().HasMaxLength(64);
            e.HasIndex(u => u.Username).IsUnique();
            e.Property(u => u.Email).IsRequired().HasMaxLength(256);
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.PasswordHash).IsRequired();
            e.Property(u => u.Role).IsRequired().HasMaxLength(32);
        });

        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.TokenHash).IsRequired();
            e.HasIndex(t => t.TokenHash).IsUnique();
        });

        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
