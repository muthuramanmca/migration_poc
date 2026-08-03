namespace Identity.Application.Dtos;

/// <summary>Never carries <c>PasswordHash</c>. <c>Role</c> is the titlecase BuildingBlocks.Security.Roles value, not Java's uppercase enum name.</summary>
public sealed record PassengerResponse(Guid Id, string Username, string Email, string Role);
