using Aero.Cms.Abstractions.Content;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Core.Content.Services;

/// <summary>
/// Defines an interface for IContentQueryService.
/// </summary>
public interface IContentQueryService
{
        /// <summary>
    /// GetByTypeAsync method.
    /// </summary>
Task<Result<(IReadOnlyList<ContentItem> Items, long TotalCount), AeroError>> GetByTypeAsync(
        long siteId, string contentTypeAlias, int skip = 0, int take = 20, CancellationToken ct = default);

        /// <summary>
    /// CountByTypeAsync method.
    /// </summary>
Task<Result<long, AeroError>> CountByTypeAsync(
        long siteId, string contentTypeAlias, CancellationToken ct = default);

        /// <summary>
    /// SearchAsync method.
    /// </summary>
Task<Result<IReadOnlyList<ContentItem>, AeroError>> SearchAsync(
        long siteId, string contentTypeAlias, Dictionary<string, string> fieldFilters, CancellationToken ct = default);

        /// <summary>
    /// ListCultureVariantsAsync method.
    /// </summary>
Task<Result<IReadOnlyList<ContentItem>, AeroError>> ListCultureVariantsAsync(
        long siteId, string contentTypeAlias, long translationGroupId, CancellationToken ct = default);
}
