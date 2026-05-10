namespace Aero.Cms.Modules.Commerce.Catalog.Events;

/// <summary>
/// Published by Catalog when all items in an order are confirmed in stock.
/// </summary>
public sealed record OrderStockConfirmed(long OrderId);
