using BuildingBlocks.Common;

namespace Identity.Domain;

/// <summary>
/// Placeholder entity -- shape only, enough for EF Core migrations to run. Real fields/validation/
/// behavior land with Identity's actual business-logic pass (registration/login), per the
/// migration plan's 02-04 per-slice pipeline; this skeleton doesn't implement that logic yet.
/// </summary>
public class User
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;

    [SensitiveData]
    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
}
