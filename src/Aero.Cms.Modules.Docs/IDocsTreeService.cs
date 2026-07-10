using Aero.Cms.Modules.Docs.Areas.Docs.Models;

namespace Aero.Cms.Modules.Docs;

/// <summary>
/// Defines an interface for IDocsTreeService.
/// </summary>
public interface IDocsTreeService
{
        /// <summary>
    /// GetSidebarTreeAsync method.
    /// </summary>
Task<Result<IReadOnlyList<DocsSidebarNode>, AeroError>> GetSidebarTreeAsync(
        long siteId,
        long activeId = 0,
        bool publishedOnly = true,
        CancellationToken ct = default);

        /// <summary>
    /// GetSidebarTreeAsync method.
    /// </summary>
Task<Result<IReadOnlyList<DocsSidebarNode>, AeroError>> GetSidebarTreeAsync(
        long siteId,
        long activeId,
        bool publishedOnly,
        string? culture,
        CancellationToken ct = default);

        /// <summary>
    /// GetBreadcrumbsAsync method.
    /// </summary>
Task<Result<IReadOnlyList<DocsPage>, AeroError>> GetBreadcrumbsAsync(
        long siteId,
        long docId,
        bool publishedOnly = true,
        CancellationToken ct = default);

        /// <summary>
    /// GetBreadcrumbsAsync method.
    /// </summary>
Task<Result<IReadOnlyList<DocsPage>, AeroError>> GetBreadcrumbsAsync(
        long siteId,
        long docId,
        bool publishedOnly,
        string? culture,
        CancellationToken ct = default);

        /// <summary>
    /// ExtractHeadings method.
    /// </summary>
IReadOnlyList<HeadingItem> ExtractHeadings(string? markdown);

        /// <summary>
    /// CreateChildSectionAsync method.
    /// </summary>
Task<Result<DocsPage, AeroError>> CreateChildSectionAsync(
        long siteId,
        long spaceId,
        long parentId,
        string title,
        string? summary,
        CancellationToken ct = default);

        /// <summary>
    /// MoveSectionAsync method.
    /// </summary>
Task<Result<DocsPage, AeroError>> MoveSectionAsync(
        long siteId,
        long spaceId,
        long sectionId,
        long newParentId,
        int? order,
        bool rewriteSlug,
        CancellationToken ct = default);

        /// <summary>
    /// ReorderSiblingsAsync method.
    /// </summary>
Task<Result<bool, AeroError>> ReorderSiblingsAsync(
        long siteId,
        long spaceId,
        long parentId,
        IReadOnlyList<long> orderedIds,
        CancellationToken ct = default);
}
