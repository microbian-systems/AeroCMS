namespace Aero.Cms.Modules.Commerce.Orders.Domain;

/// <summary>
/// Buyer associated with an order.
/// </summary>
public sealed class Buyer : Entity
{
        /// <summary>
    /// Gets or sets the Identity Id.
    /// </summary>
public string IdentityId { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public string Name { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Email.
    /// </summary>
public string Email { get; set; } = string.Empty;
}
