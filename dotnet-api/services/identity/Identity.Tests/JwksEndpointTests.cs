using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Identity.Tests;

/// <summary>
/// Skeleton-level smoke test: proves the DI container resolves and the app starts and serves a
/// request end to end, without needing a live SQL Server/RabbitMQ (this endpoint touches neither).
/// Real business-logic tests land with Identity's 04_03 pass, generated from its behavior spec.
/// </summary>
public class JwksEndpointTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task JwksEndpoint_ReturnsAKeySet()
    {
        // The dev-only startup migration is switched off -- it would demand a live SQL Server.
        // This endpoint touches no database.
        var client = factory
            .WithWebHostBuilder(builder => builder.UseSetting("RunMigrationsOnStartup", "false"))
            .CreateClient();

        var response = await client.GetAsync("/.well-known/jwks.json");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"keys\"", body);
    }
}
