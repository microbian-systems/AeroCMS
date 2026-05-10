using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Events;
using Aero.Cms.Core.Entities;
using Aero.Core;
using Aero.Core.Http;
using Aero.Core.Railway;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Pages;

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

public record BreadcrumbItem(
    long Id,
    string Title,
    string Slug,
    string Path);

public interface INavigationService
{
    /// <summary>
    /// Gets the full navigation tree for the current site.
    /// Hidden nodes are excluded and their descendants are also hidden (cascading).
    /// </summary>
    Task<Result<IReadOnlyList<NavigationNode>, AeroError>> GetNavigationTreeAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Gets breadcrumb trail from root to the given page.
    /// Uses a single query against the materialized path.
    /// </summary>
    Task<Result<BreadcrumbItem[], AeroError>> GetBreadcrumbAsync(
        long pageId, CancellationToken ct = default);

    /// <summary>
    /// Toggles the hidden state of a page. When hiding a parent, all descendants are also hidden.
    /// </summary>
    Task<Result<bool, AeroError>> SetHiddenAsync(
        long pageId, bool isHidden, CancellationToken ct = default);

    /// <summary>
    /// Marks all descendants of a hidden parent as hidden (cascade).
    /// </summary>
    Task<Result<bool, AeroError>> MarkHiddenDescendantsAsync(
        long parentId, CancellationToken ct = default);
}

public sealed class NavigationService : INavigationService
{
    private readonly IDocumentSession _session;
    private readonly ISiteContext _siteContext;
    private readonly ILogger<NavigationService> _logger;

    public NavigationService(
        IDocumentSession session,
        ISiteContext siteContext,
        ILogger<NavigationService> logger)
    {
        _session = session;
        _siteContext = siteContext;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<NavigationNode>, AeroError>> GetNavigationTreeAsync(
        CancellationToken ct = default)
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

            // Apply visibility change directly and append event for version history
            page.ShowInNavMenu = !isHidden;
            _session.Events.Append($"page-{pageId}", new PageVisibilityChanged(isHidden));
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
            return Prelude.Ok<bool, AeroError>(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set hidden state for page {PageId}", pageId);
            return Prelude.Fail<bool, AeroError>(
                AeroError.DatabaseError("Failed to update hidden state."));
        }
    }

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
                .Query<PageDocument>()
                .Where(x => x.SiteId == siteId && x.Path.StartsWith(parent.Path + "/"))
                .ToListAsync(ct);

            foreach (var descendant in descendants)
            {
                descendant.IsHidden = true;
                _session.Update(descendant);
            }

            await _session.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Marked {Count} descendants as hidden for page {PageId}",
                descendants.Count, parentId);

            return Prelude.Ok<bool, AeroError>(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark hidden descendants for page {ParentId}", parentId);
            return Prelude.Fail<bool, AeroError>(
                AeroError.DatabaseError("Failed to cascade hidden state."));
        }
    }
}
