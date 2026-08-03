using BuildingBlocks.Common;
using Identity.Application;
using Identity.Domain;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure;

public class UserRepository(IdentityDbContext dbContext) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default) =>
        dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == username, cancellationToken);

    public Task<bool> ExistsByUsernameAsync(string username, CancellationToken cancellationToken = default) =>
        dbContext.Users.AsNoTracking().AnyAsync(u => u.Username == username, cancellationToken);

    public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        dbContext.Users.AsNoTracking().AnyAsync(u => u.Email == email, cancellationToken);

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        dbContext.Users.Add(user);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // Closes the registration race condition the spec flags as unhandled in java-api:
            // IdentityService's Exists checks and this insert aren't atomic, so two concurrent
            // registrations for the same username/email can both pass the checks and collide here.
            // Re-check which constraint actually tripped so the right conflict code comes back,
            // instead of letting this surface as an unhandled 500.
            if (await dbContext.Users.AsNoTracking().AnyAsync(u => u.Username == user.Username, cancellationToken))
            {
                throw ApiException.Conflict("DUPLICATE_USERNAME", "Username is already taken");
            }

            throw ApiException.Conflict("DUPLICATE_EMAIL", "Email is already registered");
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };
}
