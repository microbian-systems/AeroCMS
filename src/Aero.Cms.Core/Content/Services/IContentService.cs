using Aero.Cms.Abstractions.Content;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Core.Content.Services;

/// <summary>
/// Defines an interface for IContentService.
/// </summary>
public interface IContentService
{
        /// <summary>
    /// LoadAsync method.
    /// </summary>
Task<Result<ContentItem, AeroError>> LoadAsync(long id, CancellationToken ct = default);
        /// <summary>
    /// GetBySlugAsync method.
    /// </summary>
Task<Result<ContentItem, AeroError>> GetBySlugAsync(long siteId, string slug, CancellationToken ct = default);
        /// <summary>
    /// GetBySlugAndTypeAsync method.
    /// </summary>
Task<Result<ContentItem, AeroError>> GetBySlugAndTypeAsync(
    long siteId,
    string contentTypeAlias,
    string culture,
    string slug,
    CancellationToken ct = default);
        /// <summary>
    /// SaveAsync method.
    /// </summary>
Task<Result<ContentItem, AeroError>> SaveAsync(ContentItem item, CancellationToken ct = default);
        /// <summary>
    /// ExistsAsync method.
    /// </summary>
Task<bool> ExistsAsync(long id, CancellationToken ct = default);
        /// <summary>
    /// DeleteAsync method.
    /// </summary>
Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken ct = default);
}
