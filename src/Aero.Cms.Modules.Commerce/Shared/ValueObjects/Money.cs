namespace Aero.Cms.Modules.Commerce.Shared.ValueObjects;

/// <summary>
/// Money value object with currency support.
/// </summary>
public sealed record Money(decimal Amount, string Currency = "USD")
{
    public static Money Zero => new(0);

    public static Money operator +(Money a, Money b)
    {
        if (a.Currency != b.Currency)
            throw new InvalidOperationException($"Cannot add {a.Currency} and {b.Currency}");

        return new Money(a.Amount + b.Amount, a.Currency);
    }

    public static Money operator -(Money a, Money b)
    {
        if (a.Currency != b.Currency)
            throw new InvalidOperationException($"Cannot subtract {a.Currency} and {b.Currency}");

        return new Money(a.Amount - b.Amount, a.Currency);
    }
}
