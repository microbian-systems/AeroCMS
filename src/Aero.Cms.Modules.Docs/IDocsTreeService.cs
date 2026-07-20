using Aero.Cms.Modules.Docs.Areas.Docs.Models;

namespace Aero.Cms.Modules.Docs;

/// <summary>
/// Builds culture-aware documentation navigation and performs scoped hierarchy mutations.
/// </summary>
/// <remarks>
/// The caller supplies the site and docs-space boundaries explicitly. Implementations return
/// validation, not-found, and persistence failures as <see cref="AeroError"/> values.
/// Authorization remains the caller's responsibility.
/// </remarks>
public interface IDocsTreeService
{
    /// <summary>
    /// Builds the published or complete sidebar hierarchy without applying a culture filter.
    /// </summary>
    /// <param name="siteId">The site whose hierarchy is loaded.</param>
    /// <param name="activeId">The page to mark active and whose ancestors are expanded.</param>
    /// <param name="publishedOnly">Whether to omit non-published pages.</param>
    /// <param name="ct">The token used for the database operation.</param>
    /// <returns>
    /// Children of the first page whose slug is <c>docs</c>, an empty list when no root exists,
    /// or a database failure.
    /// </returns>
Task<Result<IReadOnlyList<DocsSidebarNode>, AeroError>> GetSidebarTreeAsync(
        long siteId,
        long activeId = 0,
        bool publishedOnly = true,
        CancellationToken ct = default);

    /// <summary>
    /// Builds the published or complete sidebar hierarchy for a culture.
    /// </summary>
    /// <param name="siteId">The site whose hierarchy is loaded.</param>
    /// <param name="activeId">The page to mark active and whose ancestors are expanded.</param>
    /// <param name="publishedOnly">Whether to omit non-published pages.</param>
    /// <param name="culture">A .NET culture name, or <see langword="null"/> for all cultures.</param>
    /// <param name="ct">The token used for the database operation.</param>
    /// <returns>
    /// Children of the first page whose slug is <c>docs</c>, an empty list when no root exists,
    /// or a database failure.
    /// </returns>
Task<Result<IReadOnlyList<DocsSidebarNode>, AeroError>> GetSidebarTreeAsync(
        long siteId,
        long activeId,
        bool publishedOnly,
        string? culture,
        CancellationToken ct = default);

    /// <summary>
    /// Builds a breadcrumb chain without applying a culture filter.
    /// </summary>
    /// <param name="siteId">The site that must contain the page.</param>
    /// <param name="docId">The active page identifier.</param>
    /// <param name="publishedOnly">Whether to omit non-published pages.</param>
    /// <param name="ct">The token used for the database operation.</param>
    /// <returns>
    /// Ancestors followed by the active page, excluding the <c>docs</c> root; an empty list
    /// when the page is outside the loaded set; or a database failure.
    /// </returns>
Task<Result<IReadOnlyList<DocsPage>, AeroError>> GetBreadcrumbsAsync(
        long siteId,
        long docId,
        bool publishedOnly = true,
        CancellationToken ct = default);

    /// <summary>
    /// Builds a breadcrumb chain for a culture.
    /// </summary>
    /// <param name="siteId">The site that must contain the page.</param>
    /// <param name="docId">The active page identifier.</param>
    /// <param name="publishedOnly">Whether to omit non-published pages.</param>
    /// <param name="culture">A .NET culture name, or <see langword="null"/> for all cultures.</param>
    /// <param name="ct">The token used for the database operation.</param>
    /// <returns>
    /// Ancestors followed by the active page, excluding the <c>docs</c> root; an empty list
    /// when the page is outside the loaded set; or a database failure.
    /// </returns>
Task<Result<IReadOnlyList<DocsPage>, AeroError>> GetBreadcrumbsAsync(
        long siteId,
        long docId,
        bool publishedOnly,
        string? culture,
        CancellationToken ct = default);

    /// <summary>
    /// Extracts second- and third-level headings for an on-page table of contents.
    /// </summary>
    /// <param name="markdown">The Markdown source, which may be <see langword="null"/>.</param>
    /// <returns>Headings in document order with Markdig-compatible anchor identifiers.</returns>
IReadOnlyList<HeadingItem> ExtractHeadings(string? markdown);

    /// <summary>
    /// Creates a draft section beneath a parent inside a docs space.
    /// </summary>
    /// <param name="siteId">The site in which to create the section.</param>
    /// <param name="spaceId">The allowed docs-space root.</param>
    /// <param name="parentId">The parent, which must be within the space.</param>
    /// <param name="title">The non-blank title used to generate the slug.</param>
    /// <param name="summary">Optional summary text.</param>
    /// <param name="ct">The token used through persistence.</param>
    /// <returns>The persisted section, or a validation, not-found, database, or post-commit event failure.</returns>
    /// <remarks>
    /// The section is committed before creation and cache-invalidation events are published.
    /// A failure can therefore be returned after persistence succeeds.
    /// </remarks>
Task<Result<DocsPage, AeroError>> CreateChildSectionAsync(
        long siteId,
        long spaceId,
        long parentId,
        string title,
        string? summary,
        CancellationToken ct = default);

    /// <summary>
    /// Moves a section within a docs space and optionally rewrites hierarchical slugs.
    /// </summary>
    /// <param name="siteId">The site containing the hierarchy.</param>
    /// <param name="spaceId">The docs-space root that bounds both source and destination.</param>
    /// <param name="sectionId">The section to move; the space root itself is rejected.</param>
    /// <param name="newParentId">The destination parent, which cannot be the section or its descendant.</param>
    /// <param name="order">The new order, or <see langword="null"/> to append after existing siblings.</param>
    /// <param name="rewriteSlug">Whether to rewrite the section slug and descendants that share its old prefix.</param>
    /// <param name="ct">The token used through persistence.</param>
    /// <returns>The persisted section, or a validation, not-found, database, or post-commit event failure.</returns>
    /// <remarks>
    /// All changed pages are committed together before an update and cache-invalidation event pair
    /// is published for each page. Event delivery is not transactionally coupled to persistence.
    /// </remarks>
Task<Result<DocsPage, AeroError>> MoveSectionAsync(
        long siteId,
        long spaceId,
        long sectionId,
        long newParentId,
        int? order,
        bool rewriteSlug,
        CancellationToken ct = default);

    /// <summary>
    /// Assigns zero-based order values to the listed children of a parent.
    /// </summary>
    /// <param name="siteId">The site containing the hierarchy.</param>
    /// <param name="spaceId">The docs-space root that must contain the parent.</param>
    /// <param name="parentId">The parent whose children may be reordered.</param>
    /// <param name="orderedIds">
    /// Child identifiers in their desired order. The list may be a subset of the parent's children.
    /// </param>
    /// <param name="ct">The token used through persistence.</param>
    /// <returns>
    /// <see langword="true"/> after persistence, <see langword="true"/> without a write for an
    /// empty list, or a validation, not-found, database, or post-commit event failure.
    /// </returns>
Task<Result<bool, AeroError>> ReorderSiblingsAsync(
        long siteId,
        long spaceId,
        long parentId,
        IReadOnlyList<long> orderedIds,
        CancellationToken ct = default);
}
