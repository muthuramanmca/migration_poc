using Identity.Application.Dtos;

namespace Identity.Application;

public interface IIdentityService
{
    Task<PassengerResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);

    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    Task<PassengerResponse> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
}
