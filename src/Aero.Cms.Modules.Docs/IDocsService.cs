using Aero.Cms.Abstractions.Models;
using Aero.Cms.Abstractions.Requests;

namespace Aero.Cms.Modules.Docs;

/// <summary>
/// Reads and mutates documentation pages within the current site boundary.
/// </summary>
/// <remarks>
/// Implementations obtain the site from their operation context. Unless a member says
/// otherwise, persistence, cache, and transport failures are returned as
/// <see cref="AeroError"/> values instead of being thrown.
/// </remarks>
public interface IDocsService
{
    /// <summary>
    /// Gets every documentation page for the current site, including drafts and all cultures.
    /// </summary>
    /// <param name="cancellationToken">The token used for cache and database operations.</param>
    /// <returns>The site-scoped pages in display order, or a database failure.</returns>
Task<global::Aero.Core.Railway.Result<IReadOnlyList<DocsPage>, AeroError>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets published pages for the current UI culture.
    /// </summary>
    /// <param name="cancellationToken">The token used for cache and database operations.</param>
    /// <returns>The published pages in display order, or a database failure.</returns>
Task<global::Aero.Core.Railway.Result<IReadOnlyList<DocsPage>, AeroError>> GetPublishedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets published pages for a normalized culture within the current site.
    /// </summary>
    /// <param name="culture">
    /// A .NET culture name, or <see langword="null"/> to use the current UI culture.
    /// </param>
    /// <param name="cancellationToken">The token used for cache and database operations.</param>
    /// <returns>The published pages in display order, or a database failure.</returns>
Task<global::Aero.Core.Railway.Result<IReadOnlyList<DocsPage>, AeroError>> GetPublishedAsync(string? culture, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a page of published documentation and the unpaged published count.
    /// </summary>
    /// <param name="skip">The number of ordered records to omit.</param>
    /// <param name="take">The maximum number of records to return.</param>
    /// <param name="cancellationToken">The token used for cache and database operations.</param>
    /// <returns>The requested page and total count, or a database failure.</returns>
    /// <remarks>This overload does not apply a culture filter.</remarks>
Task<global::Aero.Core.Railway.Result<(IReadOnlyList<DocsPage> Items, long TotalCount), AeroError>> GetPagedAsync(int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a page by its exact slug within the current site, regardless of publication state or culture.
    /// </summary>
    /// <param name="slug">The stored slug to match.</param>
    /// <param name="cancellationToken">The token used for cache and database operations.</param>
    /// <returns>The matching page, <see langword="null"/> when absent, or a database failure.</returns>
Task<global::Aero.Core.Railway.Result<DocsPage?, AeroError>> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a published page by slug for the current UI culture.
    /// </summary>
    /// <param name="slug">The stored slug to match.</param>
    /// <param name="cancellationToken">The token used for cache and database operations.</param>
    /// <returns>The matching page, a default-culture fallback, <see langword="null"/>, or a database failure.</returns>
Task<global::Aero.Core.Railway.Result<DocsPage?, AeroError>> GetPublishedBySlugAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a published page by slug and culture, falling back to the site's default culture.
    /// </summary>
    /// <param name="slug">The stored slug to match.</param>
    /// <param name="culture">
    /// A .NET culture name, or <see langword="null"/> to use the current UI culture.
    /// </param>
    /// <param name="cancellationToken">The token used for cache and database operations.</param>
    /// <returns>The matching page, a default-culture fallback, <see langword="null"/>, or a database failure.</returns>
Task<global::Aero.Core.Railway.Result<DocsPage?, AeroError>> GetPublishedBySlugAsync(string slug, string? culture, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a page by identifier and hides records outside the current site.
    /// </summary>
    /// <param name="id">The page identifier.</param>
    /// <param name="cancellationToken">The token used for cache and database operations.</param>
    /// <returns>The page, <see langword="null"/> when absent or outside the site, or a database failure.</returns>
Task<global::Aero.Core.Railway.Result<DocsPage?, AeroError>> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads all pages whose identifiers appear in an input set.
    /// </summary>
    /// <param name="ids">The identifiers to include.</param>
    /// <param name="cancellationToken">The token used for the database operation.</param>
    /// <returns>Matching pages in provider order, or a database failure.</returns>
    /// <remarks>
    /// This operation does not apply the current-site filter. Callers must authorize every
    /// requested identifier before exposing the returned records.
    /// </remarks>
Task<global::Aero.Core.Railway.Result<IReadOnlyList<DocsPage>, AeroError>> GetByIdsAsync(long[] ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the current site's pages in the same translation group as a source page.
    /// </summary>
    /// <param name="id">The source page identifier.</param>
    /// <param name="cancellationToken">The token used for database operations.</param>
    /// <returns>Culture-ordered variants, or a not-found or database failure.</returns>
Task<global::Aero.Core.Railway.Result<IReadOnlyList<DocsPage>, AeroError>> ListCultureVariantsAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a draft translation by copying a source page's content and presentation fields.
    /// </summary>
    /// <param name="id">The source page identifier.</param>
    /// <param name="targetCulture">The target .NET culture name.</param>
    /// <param name="slug">The target slug, trimmed of surrounding whitespace and slashes.</param>
    /// <param name="cancellationToken">The token used through persistence.</param>
    /// <returns>The persisted draft, or a not-found, duplicate-culture, culture-validation, or database failure.</returns>
    /// <remarks>
    /// A translated parent is used when present; otherwise the source parent identifier is retained.
    /// Persistence completes before update events are published.
    /// </remarks>
Task<global::Aero.Core.Railway.Result<DocsPage, AeroError>> ForkToCultureAsync(long id, string targetCulture, string slug, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a documentation page from a transport request.
    /// </summary>
    /// <param name="request">The source fields for the page.</param>
    /// <param name="cancellationToken">The token used through persistence.</param>
    /// <returns>The persisted page, or a validation or database failure.</returns>
    /// <remarks>
    /// Title and slug are validated. The operation context, rather than
    /// <see cref="CreateDocRequest.SiteId"/>, determines the persisted site.
    /// </remarks>
Task<global::Aero.Core.Railway.Result<DocsPage, AeroError>> CreateAsync(CreateDocRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the editable content fields of an existing page in the current site.
    /// </summary>
    /// <param name="id">The page identifier.</param>
    /// <param name="request">The replacement content fields.</param>
    /// <param name="cancellationToken">The token used through persistence.</param>
    /// <returns>The persisted page, or a not-found or database failure.</returns>
    /// <remarks>This path does not run the create-time title and slug validation.</remarks>
Task<global::Aero.Core.Railway.Result<DocsPage, AeroError>> UpdateAsync(long id, UpdateDocRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a page after applying the current site, normalized culture, audit fields, and translation defaults.
    /// </summary>
    /// <param name="page">The mutable page to store.</param>
    /// <param name="cancellationToken">The token used through persistence.</param>
    /// <returns>The persisted page, or a database or post-commit event-publication failure.</returns>
    /// <remarks>
    /// The page is committed before creation/update and cache-invalidation events are published.
    /// Consequently, a failure result can be returned after data has been saved. Authorization is
    /// not performed by this service and must be enforced by the caller.
    /// </remarks>
Task<global::Aero.Core.Railway.Result<DocsPage, AeroError>> SaveAsync(DocsPage page, CancellationToken cancellationToken = default);

    /// <summary>
    /// Maps a view model onto a new or existing page and delegates to <see cref="SaveAsync"/>.
    /// </summary>
    /// <param name="vm">The view model to map.</param>
    /// <param name="cancellationToken">The token used through persistence.</param>
    /// <returns>The persisted page, or a database or post-commit event-publication failure.</returns>
    /// <remarks>
    /// The view model's site is overwritten by the operation context during the final save.
    /// Existing records are loaded by identifier before that site assignment, so callers must
    /// authorize the identifier before invoking this method.
    /// </remarks>
Task<global::Aero.Core.Railway.Result<DocsPage, AeroError>> SaveFromViewModelAsync(DocViewModel vm, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a page in the current site.
    /// </summary>
    /// <param name="id">The page identifier.</param>
    /// <param name="cancellationToken">The token used through persistence.</param>
    /// <returns><see langword="true"/> after deletion, or a not-found, database, or post-commit event failure.</returns>
    /// <remarks>The delete is committed before deletion and cache-invalidation events are published.</remarks>
Task<global::Aero.Core.Railway.Result<bool, AeroError>> DeleteAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current UI culture's direct children of a parent in the current site.
    /// </summary>
    /// <param name="parentId">The parent page identifier.</param>
    /// <param name="cancellationToken">The token used for cache and database operations.</param>
    /// <returns>The children in display order, or a database failure.</returns>
    /// <remarks>Publication state is not filtered.</remarks>
Task<global::Aero.Core.Railway.Result<IReadOnlyList<DocsPage>, AeroError>> GetChildrenAsync(long parentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a culture's direct children of a parent in the current site.
    /// </summary>
    /// <param name="parentId">The parent page identifier.</param>
    /// <param name="culture">
    /// A .NET culture name, or <see langword="null"/> to use the current UI culture.
    /// </param>
    /// <param name="cancellationToken">The token used for cache and database operations.</param>
    /// <returns>The children in display order, or a database failure.</returns>
    /// <remarks>Publication state is not filtered.</remarks>
Task<global::Aero.Core.Railway.Result<IReadOnlyList<DocsPage>, AeroError>> GetChildrenAsync(long parentId, string? culture, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets direct children of the current site's virtual root page whose slug is <c>docs</c>.
    /// </summary>
    /// <param name="cancellationToken">The token used for cache and database operations.</param>
    /// <returns>The children in display order, an empty list when no root exists, or a database failure.</returns>
    /// <remarks>This operation does not filter culture or publication state.</remarks>
Task<global::Aero.Core.Railway.Result<IReadOnlyList<DocsPage>, AeroError>> GetTopLevelCategoriesAsync(CancellationToken cancellationToken = default);
}
