using Identity.Domain;

namespace Identity.Application;

/// <summary>Signatures only -- implementation (Identity.Infrastructure) and real query logic land with Identity's business-logic pass.</summary>
public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task AddAsync(User user, CancellationToken cancellationToken = default);
}
