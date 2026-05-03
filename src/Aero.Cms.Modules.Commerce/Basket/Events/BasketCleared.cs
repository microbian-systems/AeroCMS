namespace Aero.Cms.Modules.Commerce.Basket.Events;

/// <summary>
/// Published when a basket is cleared (typically after order placement).
/// </summary>
public sealed record BasketCleared(string CustomerId, long OrderId);
