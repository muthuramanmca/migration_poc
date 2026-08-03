using FluentValidation;
using FlightInventory.Application.Dtos;

namespace FlightInventory.Application.Validation;

/// <summary>
/// java-api had no validator here at all -- neither annotations on the DTO nor @Valid on the
/// controller parameter -- so an empty body was a silent 200 no-op (spec rule 4.5). Requiring Delta
/// is the fix signed off in design note section 7.2.
///
/// <para>Only presence is checked. A negative Delta is legitimate (it removes seats); whether the
/// result is legal depends on current capacity, which is a service-layer concern and comes back as
/// a 409 INSUFFICIENT_SEATS, not a 400.</para>
/// </summary>
public sealed class SeatAdjustmentRequestValidator : AbstractValidator<SeatAdjustmentRequest>
{
    public SeatAdjustmentRequestValidator()
    {
        RuleFor(x => x.Delta).NotNull();
    }
}
