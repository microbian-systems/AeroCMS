using Aero.Cms.Modules.Docs.Areas.Docs.Models;

namespace Aero.Cms.Modules.Docs;

public interface IDocsTreeService
{
    Task<Result<IReadOnlyList<DocsSidebarNode>, AeroError>> GetSidebarTreeAsync(
        long siteId,
        long activeId = 0,
        bool publishedOnly = true,
        CancellationToken ct = default);

    Task<Result<IReadOnlyList<DocsSidebarNode>, AeroError>> GetSidebarTreeAsync(
        long siteId,
        long activeId,
        bool publishedOnly,
        string? culture,
        CancellationToken ct = default);

    Task<Result<IReadOnlyList<DocsPage>, AeroError>> GetBreadcrumbsAsync(
        long siteId,
        long docId,
        bool publishedOnly = true,
        CancellationToken ct = default);

    Task<Result<IReadOnlyList<DocsPage>, AeroError>> GetBreadcrumbsAsync(
        long siteId,
        long docId,
        bool publishedOnly,
        string? culture,
        CancellationToken ct = default);

    IReadOnlyList<HeadingItem> ExtractHeadings(string? markdown);

    Task<Result<DocsPage, AeroError>> CreateChildSectionAsync(
        long siteId,
        long spaceId,
        long parentId,
        string title,
        string? summary,
        CancellationToken ct = default);

    Task<Result<DocsPage, AeroError>> MoveSectionAsync(
        long siteId,
        long spaceId,
        long sectionId,
        long newParentId,
        int? order,
        bool rewriteSlug,
        CancellationToken ct = default);

    Task<Result<bool, AeroError>> ReorderSiblingsAsync(
        long siteId,
        long spaceId,
        long parentId,
        IReadOnlyList<long> orderedIds,
        CancellationToken ct = default);
}
