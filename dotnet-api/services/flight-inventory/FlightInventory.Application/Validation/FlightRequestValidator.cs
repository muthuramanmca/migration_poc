using FluentValidation;
using FlightInventory.Application.Dtos;

namespace FlightInventory.Application.Validation;

/// <summary>
/// Format-only validation, mirroring java-api's Bean Validation layer. Uniqueness (rule 4.1) is a
/// service-layer check, not a validator rule -- a 409 Conflict is a different failure mode than a
/// 400.
///
/// <para>Two deliberate departures from java-api, both signed off in design note section 7:
/// an omitted Fare is a 400 here rather than an unhandled 500, and an omitted SeatCapacity is a 400
/// rather than a silently-created zero-seat flight.</para>
///
/// <para>Not validated, faithfully to java-api (spec rule 4.10): Origin may equal Destination,
/// neither is checked against any airport list, and DepartureAt may be in the past.</para>
/// </summary>
public sealed class FlightRequestValidator : AbstractValidator<FlightRequest>
{
    public FlightRequestValidator()
    {
        // MaximumLength has no java-api counterpart -- it matches the 16-char column the skeleton's
        // FlightInventoryDbContext already declared, so an over-long number fails as a 400 rather
        // than as a database error.
        RuleFor(x => x.FlightNumber)
            .NotEmpty()
            .MaximumLength(16);

        RuleFor(x => x.Origin)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(x => x.Destination)
            .NotEmpty()
            .MaximumLength(64);

        // default(DateTimeOffset) is never a legitimate departure, so NotEmpty covers both an
        // omitted field and an explicit null -- java-api's @NotNull, same 400.
        RuleFor(x => x.DepartureAt)
            .NotEmpty();

        RuleFor(x => x.Fare)
            .GreaterThanOrEqualTo(0.01m)
            .WithMessage("Fare must be greater than zero");

        RuleFor(x => x.SeatCapacity)
            .NotNull()
            .GreaterThanOrEqualTo(0)
            .WithMessage("Initial seat capacity cannot be negative");
    }
}
