using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FlightInventory.Tests;

/// <summary>
/// Skeleton-level smoke test: proves the DI container resolves and the app starts (DbContext,
/// outbox, HoldSeat/ReleaseSeat consumers, JWT auth middleware all wire up) without needing a live
/// SQL Server/RabbitMQ/Identity -- the liveness check only touches "self". Real business-logic
/// tests land with FlightInventory's 04_03 pass, generated from its behavior spec.
/// </summary>
public class HealthEndpointTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task AliveEndpoint_ReturnsHealthy()
    {
        // Stays in Development, which is the only environment MapDefaultEndpoints maps /alive in --
        // but with the dev-only startup migration switched off, since it would demand a live SQL
        // Server. The liveness check itself only touches "self".
        var client = factory
            .WithWebHostBuilder(builder => builder.UseSetting("RunMigrationsOnStartup", "false"))
            .CreateClient();

        var response = await client.GetAsync("/alive");

        response.EnsureSuccessStatusCode();
    }
}
