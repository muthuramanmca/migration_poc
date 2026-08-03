using BuildingBlocks.Common;
using BuildingBlocks.Security;
using FlightInventory.Application;
using FlightInventory.Application.Dtos;

namespace FlightInventory.Api;

/// <summary>
/// The gateway's flights-route forwards /api/flights/{**catch-all} with no path-rewrite, so these
/// local routes are byte-identical to java-api's -- unlike Identity, nothing about the path shape
/// changes in this slice (design note section 1).
///
/// <para>Ids are Guid rather than java-api's auto-increment long, per ADR 0001's project-wide key
/// convention. That is the one place this slice cannot be wire-identical to the source.</para>
/// </summary>
public static class FlightEndpoints
{
    public static void MapFlightEndpoints(this WebApplication app)
    {
        // Browsing the schedule is public in java-api, and stays public here. AllowAnonymous is
        // explicit rather than implied so the intent survives anyone adding a fallback policy.
        app.MapGet("/api/flights", async (IFlightService flightService, CancellationToken ct) =>
                await flightService.ListAsync(ct))
            .WithName("ListFlights")
            .AllowAnonymous();

        app.MapGet("/api/flights/{id:guid}", async (Guid id, IFlightService flightService, CancellationToken ct) =>
                await flightService.GetByIdAsync(id, ct))
            .WithName("GetFlight")
            .AllowAnonymous();

        // 201, not 200 -- java-api returns ResponseEntity.status(201). The OpenAPI export says 200
        // only because springdoc can't see through ResponseEntity (spec section 2).
        app.MapPost("/api/flights", async (FlightRequest request, IFlightService flightService, CancellationToken ct) =>
            {
                var response = await flightService.CreateAsync(request, ct);
                return Results.Json(response, statusCode: StatusCodes.Status201Created);
            })
            .WithValidation<FlightRequest>()
            .WithName("CreateFlight")
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        app.MapPut("/api/flights/{id:guid}/seats", async (Guid id, SeatAdjustmentRequest request, IFlightService flightService, CancellationToken ct) =>
                await flightService.AdjustSeatsAsync(id, request, ct))
            .WithValidation<SeatAdjustmentRequest>()
            .WithName("AdjustFlightSeats")
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        // Soft delete, and 204 -- again the code, not the contract export, is authoritative.
        app.MapDelete("/api/flights/{id:guid}", async (Guid id, IFlightService flightService, CancellationToken ct) =>
            {
                await flightService.DeleteAsync(id, ct);
                return Results.NoContent();
            })
            .WithName("DeleteFlight")
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);
    }
}
