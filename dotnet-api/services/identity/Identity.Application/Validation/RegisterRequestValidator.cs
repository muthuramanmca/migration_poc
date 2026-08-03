using FluentValidation;
using Identity.Application.Dtos;

namespace Identity.Application.Validation;

/// <summary>
/// Format-only validation, matching java-api's Bean Validation layer exactly: 3-30 char username,
/// valid email shape, and the password rule from spec 4.3 (min 8 chars, at least one digit -- no
/// max length, no special-character requirement). Uniqueness (rule 4.1) is not a validator
/// concern; see <see cref="RegisterRequest"/>.
/// </summary>
public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty()
            .Length(3, 30);

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8)
            .Must(password => password.Any(char.IsDigit))
            .WithMessage("'Password' must be at least 8 characters and contain at least one digit.");
    }
}
