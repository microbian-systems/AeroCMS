namespace Aero.Cms.Modules.Commerce.Orders.Domain;

/// <summary>
/// Shipping/billing address value object.
/// </summary>
public sealed record Address
{
    public string Street { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string? State { get; init; }
    public string PostalCode { get; init; } = string.Empty;
    public string Country { get; init; } = string.Empty;
}
