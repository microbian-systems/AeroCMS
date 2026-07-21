using FluentValidation;

namespace Aero.Cms.Modules.Commerce.Payments;

public sealed class InitiatePaymentRequestValidator : AbstractValidator<InitiatePaymentRequest>
{
    public InitiatePaymentRequestValidator()
    {
        RuleFor(x => x.OrderId).GreaterThan(0);
        RuleFor(x => x.Provider).Must(x => x is "stripe" or "paypal")
            .WithMessage("Provider must be 'stripe' or 'paypal'.");
        RuleFor(x => x.IdempotencyKey)
            .NotEmpty()
            .MaximumLength(128)
            .Matches("^[A-Za-z0-9][A-Za-z0-9._:-]{0,127}$")
            .WithMessage("Idempotency key contains unsupported characters.");
    }
}
