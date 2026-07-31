using BuildingBlocks.Common;
using BuildingBlocks.Observability;
using Notification.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddBuildingBlocksSerilog(serviceName: "Notification");

builder.Services.AddNotificationInfrastructure(builder.Configuration);
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseBuildingBlocksCommon();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// No client-facing endpoints and no JWT auth wiring -- Notification has no protected surface to
// guard, it's a pure event consumer (BookingConfirmedConsumer). This skeleton proves the service
// starts and the consumer registration wires up cleanly.

app.MapDefaultEndpoints();

app.Run();

public partial class Program;
