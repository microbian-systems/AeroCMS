using System.Linq.Expressions;
using Aero.Cms.Modules.Commerce.Basket.Models;

namespace Aero.Cms.Modules.Commerce.Basket.Services;

/// <summary>
/// Provides document-store operations and customer-keyed basket mutations.
/// </summary>
/// <remarks>
/// Implementations return operational failures as <see cref="Result{T,TError}"/>. They do not infer a customer
/// from an authenticated principal, validate product data, enforce item quantities, or coordinate concurrent writers.
/// </remarks>
public interface IBasketService
{
    /// <summary>Loads a basket by document identifier.</summary>
    /// <returns>A successful result containing the basket, or <see langword="null"/> when no document exists.</returns>
    /// <param name="id">The document identifier to load.</param>
    /// <param name="ct">Cancellation token for the document-store operation.</param>
    Task<Result<BasketDocument?, AeroError>> GetByIdAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// Enumerates every persisted basket visible to the document session.
    /// </summary>
    /// <param name="ct">Cancellation token for the document-store operation.</param>
    /// <returns>A successful result containing the materialized basket list.</returns>
    Task<Result<IReadOnlyList<BasketDocument>, AeroError>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Queries baskets using a provider-translatable predicate.</summary>
    /// <param name="predicate">The predicate evaluated by the document store.</param>
    /// <param name="ct">Cancellation token for the document-store operation.</param>
    /// <returns>A successful result containing matching baskets.</returns>
    Task<Result<IReadOnlyList<BasketDocument>, AeroError>> FindAsync(Expression<Func<BasketDocument, bool>> predicate, CancellationToken ct = default);

    /// <summary>Stores a basket and commits the current document session.</summary>
    /// <param name="entity">The basket document to store.</param>
    /// <param name="ct">Cancellation token for the commit.</param>
    /// <returns>A successful result containing <paramref name="entity"/> after the commit completes.</returns>
    Task<Result<BasketDocument, AeroError>> InsertAsync(BasketDocument entity, CancellationToken ct = default);
    /// <summary>Stores the supplied basket and commits the current document session.</summary>
    /// <param name="entity">The basket document to store.</param>
    /// <param name="ct">Cancellation token for the commit.</param>
    /// <returns>A successful result containing <paramref name="entity"/> after the commit completes.</returns>
    Task<Result<BasketDocument, AeroError>> UpdateAsync(BasketDocument entity, CancellationToken ct = default);
    /// <summary>Deletes the basket document with the supplied identifier and commits the current session.</summary>
    /// <param name="id">The document identifier to delete.</param>
    /// <param name="ct">Cancellation token for the commit.</param>
    /// <returns>A successful <see langword="true"/> after the delete command is committed.</returns>
    Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken ct = default);
    /// <summary>Counts every basket visible to the document session.</summary>
    /// <param name="ct">Cancellation token for the document-store operation.</param>
    /// <returns>A successful result containing the count.</returns>
    Task<Result<long, AeroError>> CountAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the first basket with the supplied customer identifier, or creates and commits a new empty basket.
    /// </summary>
    /// <remarks>Concurrent calls can each observe no basket before either creates one; no uniqueness guarantee is enforced here.</remarks>
    /// <param name="customerId">The caller-supplied customer identifier used for lookup.</param>
    /// <param name="ct">Cancellation token for document-store operations.</param>
    /// <returns>The existing or newly committed basket, or an operational failure.</returns>
    Task<Result<BasketDocument, AeroError>> GetOrCreateBasketAsync(string customerId, CancellationToken ct = default);

    /// <summary>
    /// Adds an item to a customer-keyed basket and commits the changed basket.
    /// </summary>
    /// <remarks>When an item with the same product ID exists, only its quantity is incremented; other incoming fields are ignored. No product, price, currency, stock, or quantity validation is performed.</remarks>
    /// <param name="customerId">The caller-supplied customer identifier used for lookup.</param>
    /// <param name="item">The item snapshot to add or whose quantity to merge.</param>
    /// <param name="ct">Cancellation token for document-store operations.</param>
    /// <returns>The committed basket, or an operational failure.</returns>
    Task<Result<BasketDocument, AeroError>> AddItemAsync(string customerId, BasketItem item, CancellationToken ct = default);

    /// <summary>
    /// Removes all items with a matching product identifier and commits the basket.
    /// </summary>
    /// <param name="customerId">The caller-supplied customer identifier used for lookup.</param>
    /// <param name="productId">The product identifier to remove.</param>
    /// <param name="ct">Cancellation token for document-store operations.</param>
    /// <returns>The committed basket, including when it had no matching item.</returns>
    Task<Result<BasketDocument, AeroError>> RemoveItemAsync(string customerId, long productId, CancellationToken ct = default);

    /// <summary>
    /// Removes every item from a customer-keyed basket and commits the change.
    /// </summary>
    /// <param name="customerId">The caller-supplied customer identifier used for lookup.</param>
    /// <param name="ct">Cancellation token for document-store operations.</param>
    /// <returns>The committed empty basket, or an operational failure.</returns>
    Task<Result<BasketDocument, AeroError>> ClearBasketAsync(string customerId, CancellationToken ct = default);
}
