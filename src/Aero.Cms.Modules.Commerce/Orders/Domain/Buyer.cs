namespace Aero.Cms.Modules.Commerce.Orders.Domain;

/// <summary>
/// Buyer associated with an order.
/// </summary>
public sealed class Buyer : Entity
{
    public string IdentityId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
