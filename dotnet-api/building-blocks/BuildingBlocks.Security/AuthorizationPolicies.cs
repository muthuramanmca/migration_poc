namespace BuildingBlocks.Security;

/// <summary>
/// Named authorization policies shared across every service, registered once by
/// <see cref="JwtAuthenticationExtensions.AddBuildingBlocksJwtAuthentication"/>.
///
/// Centralised rather than declared inline per-endpoint because three slices need the identical
/// admin rule -- FlightInventory's schedule mutations (this slice), Booking's ticket/complete, and
/// Notification's log listing -- and three hand-rolled copies would be three chances to diverge.
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>Requires an authenticated caller holding <see cref="Roles.Admin"/>.</summary>
    public const string AdminOnly = "AdminOnly";
}
