using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Events;
using Aero.Cms.Data.Queries;
using Aero.Core.Http;
using ZiggyCreatures.Caching.Fusion;

namespace Aero.Cms.Modules.Pages;

/// <summary>
/// Describes a visible page in the flattened, depth-first navigation tree.
/// </summary>
/// <param name="Id">The page identifier.</param>
/// <param name="Title">The navigation title.</param>
/// <param name="Slug">The page slug.</param>
/// <param name="Path">The materialized hierarchical path.</param>
/// <param name="Depth">The zero-based hierarchy depth.</param>
/// <param name="Order">The page's sibling order.</param>
/// <param name="IsHidden">Whether the source page is marked hidden.</param>
/// <param name="ParentId">The parent page identifier, or <see langword="null"/> for a root.</param>
/// <param name="HasChildren">Whether at least one non-hidden direct child exists.</param>
public record NavigationNode(
    long Id,
    string Title,
    string Slug,
    string Path,
    int Depth,
    int Order,
    bool IsHidden,
    long? ParentId,
    bool HasChildren);

/// <summary>
/// Describes one page in a root-to-current-page breadcrumb.
/// </summary>
/// <param name="Id">The page identifier.</param>
/// <param name="Title">The breadcrumb title.</param>
/// <param name="Slug">The page slug.</param>
/// <param name="Path">The materialized hierarchical path.</param>
public record BreadcrumbItem(
    long Id,
    string Title,
    string Slug,
    string Path);

/// <summary>
/// Provides site-scoped navigation reads and hidden-state mutations.
/// </summary>
public interface INavigationService
{
    /// <summary>
    /// Gets the full navigation tree for the current site.
    /// Hidden nodes are excluded and their descendants are also hidden (cascading).
    /// When FusionCache is available, the complete result, including an error result,
    /// is cached under the current site key using the cache's configured defaults.
    /// </summary>
    /// <param name="ct">The token used when a store query is required.</param>
    /// <returns>The visible flattened navigation tree or a database error.</returns>
    Task<Result<IReadOnlyList<NavigationNode>, AeroError>> GetNavigationTreeAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Gets breadcrumb trail from root to the given page.
    /// Uses a single query against the materialized path.
    /// </summary>
    /// <param name="pageId">The current page identifier.</param>
    /// <param name="ct">The token used for store queries.</param>
    /// <returns>
    /// The ordered root-to-page breadcrumb, a not-found error for a page outside the
    /// current site, or a database error.
    /// </returns>
    Task<Result<BreadcrumbItem[], AeroError>> GetBreadcrumbAsync(
        long pageId, CancellationToken ct = default);

    /// <summary>
    /// Toggles the hidden state of a page. When hiding a parent, all descendants are also hidden.
    /// Evicts the navigation tree cache on success.
    /// </summary>
    /// <param name="pageId">The page identifier.</param>
    /// <param name="isHidden">Whether the page should be hidden.</param>
    /// <param name="ct">The token used for persistence and any descendant cascade.</param>
    /// <returns>A successful result after persistence, or a not-found/database error.</returns>
    /// <remarks>
    /// The page itself is saved before a descendant cascade begins. A cascade failure
    /// can therefore be returned after the parent state has already been committed.
    /// Showing a parent does not unhide descendants.
    /// </remarks>
    Task<Result<bool, AeroError>> SetHiddenAsync(
        long pageId, bool isHidden, CancellationToken ct = default);

    /// <summary>
    /// Marks all descendants of a hidden parent as hidden (cascade).
    /// Uses the compiled PagesByPathPrefixQuery for efficient materialized-path prefix matching.
    /// Evicts the navigation tree cache on success.
    /// </summary>
    /// <param name="parentId">The parent page identifier.</param>
    /// <param name="ct">The token used for queries and persistence.</param>
    /// <returns>A successful result after persistence, or a not-found/database error.</returns>
    Task<Result<bool, AeroError>> MarkHiddenDescendantsAsync(
        long parentId, CancellationToken ct = default);
}

/// <summary>
/// Implements navigation operations over site-scoped Sable page documents.
/// </summary>
/// <remarks>
/// Public operations catch cancellation exceptions raised inside their bodies and
/// translate them to database-error results.
/// </remarks>
public sealed class NavigationService : INavigationService
{
    private readonly IDocumentSession _session;
    private readonly ISiteContext _siteContext;
    private readonly ILogger<NavigationService> _logger;
    private readonly IFusionCache? _cache;
    private static readonly TimeSpan NavCacheDuration = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Initializes a new instance of the <see cref="NavigationService"/> class.
    /// </summary>
    /// <param name="session">The scoped Sable document session.</param>
    /// <param name="siteContext">The current site scope.</param>
    /// <param name="logger">The navigation logger.</param>
    /// <param name="cache">The optional navigation cache.</param>
public NavigationService(
        IDocumentSession session,
        ISiteContext siteContext,
        ILogger<NavigationService> logger,
        IFusionCache? cache = null)
    {
        _session = session;
        _siteContext = siteContext;
        _logger = logger;
        _cache = cache;
    }

    // ── Navigation Tree ───────────────────────────────────────────────────

    /// <inheritdoc />
public async Task<Result<IReadOnlyList<NavigationNode>, AeroError>> GetNavigationTreeAsync(
        CancellationToken ct = default)
    {
        if (_cache is not null)
        {
            var key = $"nav:tree:{_siteContext.SiteId}";
            var cached = await _cache.TryGetAsync<Result<IReadOnlyList<NavigationNode>, AeroError>>(key);
            if (cached.HasValue)
                return cached.Value;

            var result = await LoadNavigationTreeAsync(ct);
            await _cache.SetAsync(key, result, token: ct);
            return result;
        }

        return await LoadNavigationTreeAsync(ct);
    }

    private async Task<Result<IReadOnlyList<NavigationNode>, AeroError>> LoadNavigationTreeAsync(
        CancellationToken ct)
    {
        try
        {
            var siteId = _siteContext.SiteId;

            // Load all published, non-deleted pages for the site
            var pages = await _session
                .Query<PageDocument>()
                .Where(x => x.SiteId == siteId
                    && x.PublicationState == ContentPublicationState.Published
                    && x.Deleted == false)
                .OrderBy(x => x.Path)
                .ToListAsync(ct);

            // Build hidden parent set for cascade logic
            var hiddenParentIds = new HashSet<long>();
            foreach (var page in pages)
            {
                if (page.IsHidden)
                    hiddenParentIds.Add(page.Id);
            }

            // Build a lookup of which IDs have children
            var childCounts = pages
                .GroupBy(x => x.ParentId)
                .ToDictionary(g => g.Key ?? -1, g => g.Count());

            var nodes = new List<NavigationNode>(pages.Count);
            foreach (var page in pages)
            {
                // Cascade: if any ancestor is hidden, this page is also excluded
                if (page.IsHidden)
                {
                    hiddenParentIds.Add(page.Id);
                    continue; // Hidden pages are excluded from navigation
                }

                // Check if this page is a descendant of any hidden page
                var isDescendantOfHidden = false;
                if (page.Path.Length > 1)
                {
                    var segments = page.Path.TrimStart('/').Split('/');
                    var currentPath = "";
                    for (var i = 0; i < segments.Length - 1; i++)
                    {
                        currentPath += "/" + segments[i];
                        // Find the ancestor at this path
                        var ancestor = pages.FirstOrDefault(p => p.Path == currentPath);
                        if (ancestor is { IsHidden: true })
                        {
                            isDescendantOfHidden = true;
                            break;
                        }
                    }
                }

                if (isDescendantOfHidden)
                    continue;

                var hasChildren = childCounts.TryGetValue(page.Id, out var count) && count > 0
                    && pages.Any(p => p.ParentId == page.Id && !p.IsHidden);

                nodes.Add(new NavigationNode(
                    Id: page.Id,
                    Title: page.Title,
                    Slug: page.Slug,
                    Path: page.Path,
                    Depth: page.Depth,
                    Order: page.Order,
                    IsHidden: page.IsHidden,
                    ParentId: page.ParentId,
                    HasChildren: hasChildren));
            }

            return Prelude.Ok<IReadOnlyList<NavigationNode>, AeroError>(nodes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load navigation tree for site {SiteId}", _siteContext.SiteId);
            return Prelude.Fail<IReadOnlyList<NavigationNode>, AeroError>(
                AeroError.DatabaseError("Failed to load navigation tree."));
        }
    }

    // ── Breadcrumb ────────────────────────────────────────────────────────

    /// <inheritdoc />
public async Task<Result<BreadcrumbItem[], AeroError>> GetBreadcrumbAsync(
        long pageId, CancellationToken ct = default)
    {
        try
        {
            var siteId = _siteContext.SiteId;
            var page = await _session.LoadAsync<PageDocument>(pageId, ct);

            if (page is null || page.SiteId != siteId)
            {
                return Prelude.Fail<BreadcrumbItem[], AeroError>(
                    AeroError.NotFoundError($"Page {pageId} not found."));
            }

            // Root page: only the page itself
            if (page.Depth == 0)
            {
                return Prelude.Ok<BreadcrumbItem[], AeroError>(
                [
                    new BreadcrumbItem(page.Id, page.Title, page.Slug, page.Path)
                ]);
            }

            // Build ancestor paths from the materialized path
            var segments = page.Path.TrimStart('/').Split('/');
            var ancestorPaths = new string[segments.Length - 1];
            for (var i = 1; i < segments.Length; i++)
            {
                ancestorPaths[i - 1] = "/" + string.Join("/", segments.Take(i));
            }

            // Single query: load all ancestors
            var ancestors = await _session
                .Query<PageDocument>()
                .Where(x => x.SiteId == siteId && ancestorPaths.Contains(x.Path))
                .OrderBy(x => x.Depth)
                .Select(x => new { x.Id, x.Title, x.Slug, x.Path })
                .ToListAsync(ct);

            var breadcrumb = new BreadcrumbItem[ancestors.Count + 1];
            for (var i = 0; i < ancestors.Count; i++)
            {
                breadcrumb[i] = new BreadcrumbItem(
                    ancestors[i].Id, ancestors[i].Title, ancestors[i].Slug, ancestors[i].Path);
            }

            breadcrumb[^1] = new BreadcrumbItem(page.Id, page.Title, page.Slug, page.Path);

            return Prelude.Ok<BreadcrumbItem[], AeroError>(breadcrumb);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load breadcrumb for page {PageId}", pageId);
            return Prelude.Fail<BreadcrumbItem[], AeroError>(
                AeroError.DatabaseError("Failed to load breadcrumb."));
        }
    }

    // ── Hide / Cascade ────────────────────────────────────────────────────

    /// <inheritdoc />
public async Task<Result<bool, AeroError>> SetHiddenAsync(
        long pageId, bool isHidden, CancellationToken ct = default)
    {
        try
        {
            var siteId = _siteContext.SiteId;

            var page = await _session.LoadAsync<PageDocument>(pageId, ct);

            if (page is null || page.SiteId != siteId)
            {
                return Prelude.Fail<bool, AeroError>(
                    AeroError.NotFoundError($"Page {pageId} not found."));
            }

            page.IsHidden = isHidden;
            page.ShowInNavMenu = !isHidden;
            page.ModifiedOn = DateTimeOffset.UtcNow;
            _session.Store(page);
            await _session.SaveChangesAsync(ct);

            // Cascade: if hiding a parent, also hide all descendants (direct update for batch efficiency)
            if (isHidden)
            {
                var cascadeResult = await MarkHiddenDescendantsAsync(pageId, ct);
                if (cascadeResult is Result<bool, AeroError>.Failure f)
                    return f;
            }

            _logger.LogInformation("Page {PageId} hidden state set to {IsHidden}", pageId, isHidden);

            EvictNavigationCache();
            return Prelude.Ok<bool, AeroError>(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set hidden state for page {PageId}", pageId);
            return Prelude.Fail<bool, AeroError>(
                AeroError.DatabaseError("Failed to update hidden state."));
        }
    }


    /// <inheritdoc />
public async Task<Result<bool, AeroError>> MarkHiddenDescendantsAsync(
        long parentId, CancellationToken ct = default)
    {
        try
        {
            var siteId = _siteContext.SiteId;

            // Load the parent to get its path
            var parent = await _session.LoadAsync<PageDocument>(parentId, ct);
            if (parent is null || parent.SiteId != siteId)
            {
                return Prelude.Fail<bool, AeroError>(
                    AeroError.NotFoundError($"Page {parentId} not found."));
            }

            var descendants = await _session
                .QueryAsync(new PagesByPathPrefixQuery
                {
                    SiteId = siteId,
                    PathPrefix = parent.Path + "/"
                }, ct);

            foreach (var descendant in descendants)
            {
                descendant.IsHidden = true;
                _session.Update(descendant);
            }

            await _session.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Marked {Count} descendants as hidden for page {PageId}",
                descendants.Count, parentId);

            EvictNavigationCache();
            return Prelude.Ok<bool, AeroError>(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark hidden descendants for page {ParentId}", parentId);
            return Prelude.Fail<bool, AeroError>(
                AeroError.DatabaseError("Failed to cascade hidden state."));
        }
    }

    // ── Cache Helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Evicts the navigation tree cache for the current site.
    /// Called after any write operation that affects navigation (hide, move, publish, etc.).
    /// </summary>
    private void EvictNavigationCache()
    {
        if (_cache is not null)
        {
            var key = $"nav:tree:{_siteContext.SiteId}";
            _cache.Remove(key);
        }
    }
}
