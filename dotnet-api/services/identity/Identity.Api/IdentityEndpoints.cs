using System.Security.Claims;
using BuildingBlocks.Common;
using Identity.Application;
using Identity.Application.Dtos;
using Microsoft.AspNetCore.Http;

namespace Identity.Api;

/// <summary>
/// The gateway's identity-route forwards /api/identity/{**catch-all} with no path-rewrite, so
/// these local routes must literally include the /api/identity prefix -- they are not
/// /api/auth/... like java-api (see this slice's design note §1).
/// </summary>
public static class IdentityEndpoints
{
    public static void MapIdentityEndpoints(this WebApplication app)
    {
        app.MapPost("/api/identity/auth/register", async (RegisterRequest request, IIdentityService identityService, CancellationToken ct) =>
            {
                var response = await identityService.RegisterAsync(request, ct);
                return Results.Json(response, statusCode: StatusCodes.Status201Created);
            })
            .WithValidation<RegisterRequest>()
            .WithName("Register")
            .AllowAnonymous();

        app.MapPost("/api/identity/auth/login", async (LoginRequest request, IIdentityService identityService, CancellationToken ct) =>
                await identityService.LoginAsync(request, ct))
            .WithValidation<LoginRequest>()
            .WithName("Login")
            .AllowAnonymous();

        // Resolves via the token's Name claim (set to username at issuance), not a path parameter
        // or a re-check against the database's current role -- spec rule 4.7.
        app.MapGet("/api/identity/passengers/me", async (ClaimsPrincipal user, IIdentityService identityService, CancellationToken ct) =>
            {
                var username = user.Identity?.Name
                    ?? throw ApiException.Unauthorized("INVALID_CREDENTIALS", "No authenticated user.");
                return await identityService.GetByUsernameAsync(username, ct);
            })
            .WithName("Me")
            .RequireAuthorization();
    }
}
