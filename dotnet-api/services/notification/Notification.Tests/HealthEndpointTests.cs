using Microsoft.AspNetCore.Mvc.Testing;

namespace Notification.Tests;

/// <summary>
/// Skeleton-level smoke test: proves the DI container resolves and the app starts (DbContext and
/// the BookingConfirmedConsumer registration wire up) without needing a live SQL Server/RabbitMQ.
/// </summary>
public class HealthEndpointTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task AliveEndpoint_ReturnsHealthy()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/alive");

        response.EnsureSuccessStatusCode();
    }
}
