using BuildingBlocks.Common;
using BuildingBlocks.Observability;
using BuildingBlocks.Security;
using FlightInventory.Api;
using FlightInventory.Application;
using FlightInventory.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddBuildingBlocksSerilog(serviceName: "FlightInventory");

builder.Services.AddFlightInventoryInfrastructure(builder.Configuration);
builder.Services.AddFlightInventoryApplication(builder.Configuration);
builder.Services.AddBuildingBlocksJwtAuthentication(builder.Configuration);
builder.Services.AddOpenApi();

var app = builder.Build();

// Dev-only, matching Identity: a real deployment applies migrations as its own release step, not on
// app startup. FlightInventory is the second service to actually read/write its database.
// Switchable off so in-process smoke tests can start the app without a live SQL Server, while still
// running as Development -- the environment the health endpoints are mapped in.
if (app.Environment.IsDevelopment() && app.Configuration.GetValue("RunMigrationsOnStartup", true))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<FlightInventoryDbContext>().Database.MigrateAsync();
}

app.UseBuildingBlocksCommon();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapFlightEndpoints();

app.MapDefaultEndpoints();

app.Run();

public partial class Program;
