using Aero.Cms.Abstractions.Content;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Core.Content.Services;

/// <summary>
/// Provides Sable-backed persistence operations for content items.
/// </summary>
/// <remarks>
/// Missing items are represented by failed results where the return type permits it.
/// Cancellation and storage-provider failures are not converted to <see cref="AeroError"/>
/// values and may propagate to the caller.
/// </remarks>
public interface IContentService
{
    /// <summary>Loads a content item by site and identifier.</summary>
    /// <param name="id">The content-item identifier.</param><param name="ct">A token that can cancel the operation.</param>
    /// <returns>
    /// The item on success, or a failed result when no item has the specified identifier.
    /// </returns>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> is canceled.</exception>
    Task<Result<ContentItem, AeroError>> LoadAsync(long siteId, long id, CancellationToken ct = default);

    /// <summary>Loads a site-scoped content item by its slug.</summary>
    /// <param name="siteId">The owning site identifier.</param><param name="slug">The item's slug.</param><param name="ct">A token that can cancel the operation.</param>
    /// <returns>The matching item, or a failed result when no item matches.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> is canceled.</exception>
    Task<Result<ContentItem, AeroError>> GetBySlugAsync(long siteId, string slug, CancellationToken ct = default);

    /// <summary>Loads a site-scoped content item using its type, culture, and slug.</summary>
    /// <param name="siteId">The owning site identifier.</param><param name="contentTypeAlias">The content type alias.</param><param name="culture">The content culture.</param><param name="slug">The item's slug.</param><param name="ct">A token that can cancel the operation.</param>
    /// <returns>The matching item, or a failed result when no item matches.</returns>
    /// <remarks>
    /// Implementations normalize <paramref name="culture"/> through <see cref="System.Globalization.CultureInfo"/>;
    /// blank input becomes <c>en-US</c>.
    /// </remarks>
    /// <exception cref="System.Globalization.CultureNotFoundException">
    /// <paramref name="culture"/> is nonblank and is not a recognized culture name.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> is canceled.</exception>
    Task<Result<ContentItem, AeroError>> GetBySlugAndTypeAsync(
    long siteId,
    string contentTypeAlias,
    string culture,
    string slug,
    CancellationToken ct = default);
    /// <summary>Stores and commits a content item.</summary>
    /// <param name="item">The item to persist.</param><param name="ct">A token that can cancel the operation.</param>
    /// <returns>A successful result containing the same item instance after the commit completes.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> is canceled.</exception>
    Task<Result<ContentItem, AeroError>> SaveAsync(ContentItem item, CancellationToken ct = default);

    /// <summary>Determines whether a content item exists in a site.</summary>
    /// <param name="id">The content-item identifier.</param><param name="ct">A token that can cancel the operation.</param>
    /// <returns><see langword="true"/> when the item exists; otherwise <see langword="false"/>.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> is canceled.</exception>
    Task<bool> ExistsAsync(long siteId, long id, CancellationToken ct = default);

    /// <summary>Deletes a content item only when it belongs to the supplied site.</summary>
    /// <param name="id">The content-item identifier.</param><param name="ct">A token that can cancel the operation.</param>
    /// <returns>A successful result containing <see langword="true"/> after the commit completes.</returns>
    /// <remarks>The current-site document is loaded and deleted as one scoped operation. Missing or
    /// foreign-site identifiers return a failed result without deleting data.</remarks>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> is canceled.</exception>
    Task<Result<bool, AeroError>> DeleteAsync(long siteId, long id, CancellationToken ct = default);
}
