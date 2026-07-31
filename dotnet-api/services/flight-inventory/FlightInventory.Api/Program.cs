using BuildingBlocks.Common;
using BuildingBlocks.Observability;
using BuildingBlocks.Security;
using FlightInventory.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddBuildingBlocksSerilog(serviceName: "FlightInventory");

builder.Services.AddFlightInventoryInfrastructure(builder.Configuration);
builder.Services.AddBuildingBlocksJwtAuthentication(builder.Configuration);
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseBuildingBlocksCommon();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// Real flight/fare/availability endpoints are deferred to FlightInventory's business-logic pass
// (migration plan steps 02-04 for this slice). This skeleton proves the service starts, the
// DbContext + outbox + HoldSeat/ReleaseSeat consumers wire up, and auth middleware is in place.

app.MapDefaultEndpoints();

app.Run();

public partial class Program;
