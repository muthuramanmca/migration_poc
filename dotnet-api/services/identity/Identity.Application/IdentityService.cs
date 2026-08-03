using BuildingBlocks.Common;
using BuildingBlocks.Contracts;
using BuildingBlocks.Security;
using Identity.Application.Dtos;
using Identity.Domain;
using MassTransit;

namespace Identity.Application;

public sealed class IdentityService(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IJwtTokenIssuer tokenIssuer,
    IPublishEndpoint publishEndpoint) : IIdentityService
{
    public async Task<PassengerResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        // Username checked before email -- if both are duplicate, only DUPLICATE_USERNAME comes
        // back, never DUPLICATE_EMAIL (spec rule 4.1).
        if (await userRepository.ExistsByUsernameAsync(request.Username, cancellationToken))
        {
            throw ApiException.Conflict("DUPLICATE_USERNAME", "Username is already taken");
        }

        if (await userRepository.ExistsByEmailAsync(request.Email, cancellationToken))
        {
            throw ApiException.Conflict("DUPLICATE_EMAIL", "Email is already registered");
        }

        // Role is never client-suppliable -- RegisterRequest has no Role field, so PASSENGER is
        // hardcoded here, matching spec rule 4.2. Neither Agent nor Admin is reachable through
        // any API in this app.
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = request.Username,
            Email = request.Email,
            PasswordHash = passwordHasher.Hash(request.Password),
            Role = Roles.Passenger,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        // Published before AddAsync's SaveChangesAsync so the outbox commits this message
        // atomically with the User insert once persisted -- the after-commit fix for the "fires
        // inside the open transaction, before commit" antipattern spec rule 4.8 flags in java-api.
        await publishEndpoint.Publish(new PassengerRegisteredEvent(user.Id, user.Username, user.CreatedAtUtc), cancellationToken);

        // May itself throw ApiException.Conflict if a concurrent registration wins the race
        // between the Exists checks above and this insert -- see UserRepository.AddAsync.
        await userRepository.AddAsync(user, cancellationToken);

        return ToResponse(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByUsernameAsync(request.Username, cancellationToken);

        // Deliberately identical for "unknown username" vs "wrong password" (spec rule 4.5) --
        // a security property, not an oversight to "improve".
        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw ApiException.Unauthorized("INVALID_CREDENTIALS", "Invalid username or password");
        }

        var issued = tokenIssuer.IssueToken(user.Username, user.Role);
        return new AuthResponse(issued.Token, issued.ExpiresInSeconds);
    }

    public async Task<PassengerResponse> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        // Fresh DB read -- returns the passenger's *current* role. The JWT's own role claim
        // (used for authorization elsewhere) is not re-checked against the database and stays
        // stale until the token expires (spec rule 4.7); this endpoint's response is not that claim.
        var user = await userRepository.GetByUsernameAsync(username, cancellationToken)
            ?? throw ApiException.NotFound("PASSENGER_NOT_FOUND", $"Passenger not found: {username}");

        return ToResponse(user);
    }

    private static PassengerResponse ToResponse(User user) => new(user.Id, user.Username, user.Email, user.Role);
}
