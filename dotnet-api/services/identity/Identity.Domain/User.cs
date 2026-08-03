using BuildingBlocks.Common;

namespace Identity.Domain;

/// <summary>Matches behavior spec §3/§4 field-for-field; see this slice's 04_01 design note §3 for the schema rationale.</summary>
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
