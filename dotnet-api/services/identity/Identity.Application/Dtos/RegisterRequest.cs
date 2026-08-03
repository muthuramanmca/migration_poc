namespace Identity.Application.Dtos;

/// <summary>
/// No <c>Role</c> field, deliberately -- role can never be client-supplied at signup (spec rule
/// 4.2). Format is enforced by <see cref="RegisterRequestValidator"/>; uniqueness (rule 4.1) is a
/// service-layer check, not a validator rule, since a 409 Conflict is a different failure mode
/// than a 400 validation error.
/// </summary>
public sealed record RegisterRequest(string Username, string Email, string Password);
