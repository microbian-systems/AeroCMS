namespace Aero.Cms.Modules.Commerce.Catalog.Events;

/// <summary>
/// Published by Catalog when one or more items in an order are out of stock.
/// </summary>
public sealed record OrderStockRejected(long OrderId, List<long> OutOfStockProductIds);
