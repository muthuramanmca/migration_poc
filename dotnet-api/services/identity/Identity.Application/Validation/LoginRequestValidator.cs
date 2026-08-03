using FluentValidation;
using Identity.Application.Dtos;

namespace Identity.Application.Validation;

/// <summary>Presence only -- no format/strength check on login (spec rule 4.3 applies to registration only).</summary>
public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Username).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
    }
}
