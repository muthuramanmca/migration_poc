using BuildingBlocks.Common;
using BuildingBlocks.Observability;
using BuildingBlocks.Security;
using Booking.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddBuildingBlocksSerilog(serviceName: "Booking");

builder.Services.AddBookingInfrastructure(builder.Configuration);
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

// Real booking-creation endpoints are deferred to Booking's business-logic pass (migration plan
// steps 02-04 for this slice). This skeleton proves the service starts, the DbContext + outbox +
// BookingCreationSaga wire up (saga persistence registered against BookingDbContext), and auth
// middleware is in place. BookingCreationSagaTests exercises the saga directly via MassTransit's
// test harness, without needing a live HTTP endpoint or broker.

app.MapDefaultEndpoints();

app.Run();

public partial class Program;
