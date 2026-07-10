namespace Aero.Cms.Modules.Commerce.Orders.Domain;

/// <summary>
/// Shipping/billing address value object.
/// </summary>
public sealed record Address
{
        /// <summary>
    /// Gets or sets the Street.
    /// </summary>
public string Street { get; init; } = string.Empty;
        /// <summary>
    /// Gets or sets the City.
    /// </summary>
public string City { get; init; } = string.Empty;
        /// <summary>
    /// Gets or sets the State.
    /// </summary>
public string? State { get; init; }
        /// <summary>
    /// Gets or sets the Postal Code.
    /// </summary>
public string PostalCode { get; init; } = string.Empty;
        /// <summary>
    /// Gets or sets the Country.
    /// </summary>
public string Country { get; init; } = string.Empty;
}
