namespace Aero.Cms.Modules.Commerce.Shared.ValueObjects;

/// <summary>
/// Represents an immutable monetary amount in a named currency.
/// </summary>
/// <param name="Amount">The amount to represent. This value is not rounded or otherwise normalized.</param>
/// <param name="Currency">The currency identifier. The value defaults to <c>USD</c> and is compared exactly during arithmetic.</param>
/// <remarks>
/// This type does not validate currency identifiers, apply exchange rates, calculate tax, or impose a scale on
/// <paramref name="Amount"/>. Callers are responsible for those domain rules before constructing or combining values.
/// </remarks>
public sealed record Money(decimal Amount, string Currency = "USD")
{
    /// <summary>
    /// Gets a zero-valued amount in the default <c>USD</c> currency.
    /// </summary>
    public static Money Zero => new(0);

    /// <summary>
    /// Adds two amounts expressed in the same currency.
    /// </summary>
    /// <param name="a">The amount to which <paramref name="b"/> is added.</param>
    /// <param name="b">The amount to add.</param>
    /// <returns>A new amount with the sum and the currency of <paramref name="a"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the two currency identifiers differ.</exception>
    public static Money operator +(Money a, Money b)
    {
        if (a.Currency != b.Currency)
            throw new InvalidOperationException($"Cannot add {a.Currency} and {b.Currency}");

        return new Money(a.Amount + b.Amount, a.Currency);
    }

    /// <summary>
    /// Subtracts one amount from another when both are expressed in the same currency.
    /// </summary>
    /// <param name="a">The amount from which <paramref name="b"/> is subtracted.</param>
    /// <param name="b">The amount to subtract.</param>
    /// <returns>A new amount with the difference and the currency of <paramref name="a"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the two currency identifiers differ.</exception>
    public static Money operator -(Money a, Money b)
    {
        if (a.Currency != b.Currency)
            throw new InvalidOperationException($"Cannot subtract {a.Currency} and {b.Currency}");

        return new Money(a.Amount - b.Amount, a.Currency);
    }
}
