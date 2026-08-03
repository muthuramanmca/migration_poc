using BuildingBlocks.Common;
using BuildingBlocks.Observability;
using BuildingBlocks.Security;
using Identity.Api;
using Identity.Application;
using Identity.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddBuildingBlocksSerilog(serviceName: "Identity");

builder.Services.AddIdentityInfrastructure(builder.Configuration);
builder.Services.AddIdentityApplication();

// Identity is both the token issuer (below) and, for /me, a resource server -- ADR 0001's
// zero-trust rule says every service validates independently, Identity included.
builder.Services.AddBuildingBlocksJwtAuthentication(builder.Configuration);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddOpenApi();

var app = builder.Build();

// No service in this solution has ever auto-applied EF Core migrations -- fine while every
// other slice was skeleton-only, but Identity is the first to actually read/write its database.
// Dev-only: a real deployment applies migrations as its own release step, not on app startup.
// Switchable off so in-process smoke tests can start the app without a live SQL Server, while still
// running as Development -- the environment the health endpoints are mapped in.
if (app.Environment.IsDevelopment() && app.Configuration.GetValue("RunMigrationsOnStartup", true))
{
    try
    {
        using var scope = app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Skipping database migrate in Development environment: {Message}", ex.Message);
    }
}

app.UseBuildingBlocksCommon();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Identity API v1");
    });
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// Every other service (and Identity.Api itself) discovers signing keys via these two endpoints --
// JwtBearerOptions.Authority triggers metadata discovery against openid-configuration first, which
// points at jwks_uri, rather than hitting jwks.json directly (BuildingBlocks.Security, consumed as-is).
app.MapGet("/.well-known/jwks.json", (RsaSigningKeyProvider keyProvider) => Results.Json(keyProvider.GetPublicJwks()))
   .WithName("GetJwks")
   .AllowAnonymous();

app.MapGet("/.well-known/openid-configuration", (IConfiguration configuration) =>
{
    var issuer = configuration["Identity:Authority"]
        ?? throw new InvalidOperationException("Configuration value 'Identity:Authority' is required.");

    return Results.Json(new { issuer, jwks_uri = $"{issuer}/.well-known/jwks.json" });
})
.WithName("OpenIdConfiguration")
.AllowAnonymous();

app.MapIdentityEndpoints();

app.MapDefaultEndpoints();

app.Run();

public partial class Program;
