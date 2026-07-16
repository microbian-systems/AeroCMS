using Aero.Cms.Abstractions.Events;
using Aero.Cms.Data.Queries;
using Aero.Core.Http;
using Wolverine;

namespace Aero.Cms.Modules.Pages;

/// <summary>
/// Defines an interface for IPageTreeService.
/// </summary>
public interface IPageTreeService
{
    /// <summary>
    /// Gets the full tree for the current site, depth-first, ordered.
    /// </summary>
    Task<Result<IReadOnlyList<PageDocument>, AeroError>> GetTreeAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets immediate children of a parent page.
    /// </summary>
    Task<Result<IReadOnlyList<PageDocument>, AeroError>> GetChildrenAsync(
        long? parentId = null, CancellationToken ct = default);

    /// <summary>
    /// Gets all ancestors from root to the given page (breadcrumb).
    /// </summary>
    Task<Result<IReadOnlyList<PageDocument>, AeroError>> GetAncestorsAsync(
        long pageId, CancellationToken ct = default);

    /// <summary>
    /// Moves a page under a new parent (or to root). Validates no circular reference.
    /// </summary>
    Task<Result<PageDocument, AeroError>> MoveAsync(
        long pageId, long? newParentId, int? order = null, CancellationToken ct = default);

    /// <summary>
    /// Creates the hierarchy path for a new/updated page.
    /// Use <paramref name="excludePageId"/> when editing an existing page
    /// to prevent the page from conflicting with itself.
    /// Returns the computed Path and Depth.
    /// </summary>
    Task<Result<(string Path, int Depth), AeroError>> ComputePathAsync(
        long siteId, long? parentId, string slug,
        long? excludePageId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the next available Order value for siblings (max + 1).
    /// </summary>
    Task<Result<int, AeroError>> GetNextSiblingOrderAsync(
        long siteId, long? parentId, CancellationToken ct = default);

    /// <summary>
    /// Updates the Path for a page and all its descendants after a slug or parent change.
    /// </summary>
    Task<Result<bool, AeroError>> UpdateDescendantPathsAsync(
        long pageId, string oldPath, string newPath, CancellationToken ct = default);
}

/// <summary>
/// Represents a class for PageTreeService.
/// </summary>
public sealed class PageTreeService : IPageTreeService
{
    private readonly IDocumentSession _session;
    private readonly ISiteContext _siteContext;
    private readonly IMessageBus _bus;
    private readonly ILogger<PageTreeService> _logger;

        /// <summary>
    /// Initializes a new instance of the <see cref="PageTreeService"/> class.
    /// </summary>
public PageTreeService(
        IDocumentSession session,
        ISiteContext siteContext,
        IMessageBus bus,
        ILogger<PageTreeService> logger)
    {
        _session = session;
        _siteContext = siteContext;
        _bus = bus;
        _logger = logger;
    }

        /// <summary>
    /// GetTreeAsync method.
    /// </summary>
public async Task<Result<IReadOnlyList<PageDocument>, AeroError>> GetTreeAsync(CancellationToken ct = default)
    {
        try
        {
            var siteId = _siteContext.SiteId;
            var pages = await _session
                .Query<PageDocument>()
                .Where(x => x.SiteId == siteId)
                .OrderBy(x => x.Path)
                .ToListAsync(ct);

            return Prelude.Ok<IReadOnlyList<PageDocument>, AeroError>(pages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load page tree for site {SiteId}", _siteContext.SiteId);
            return Prelude.Fail<IReadOnlyList<PageDocument>, AeroError>(
                AeroError.DatabaseError("Failed to load page tree."));
        }
    }

        /// <summary>
    /// GetChildrenAsync method.
    /// </summary>
public async Task<Result<IReadOnlyList<PageDocument>, AeroError>> GetChildrenAsync(
        long? parentId = null, CancellationToken ct = default)
    {
        try
        {
            var siteId = _siteContext.SiteId;
            var children = await _session
                .Query<PageDocument>()
                .Where(x => x.SiteId == siteId && x.ParentId == parentId)
                .OrderBy(x => x.Order)
                .ThenBy(x => x.Title)
                .ToListAsync(ct);

            return Prelude.Ok<IReadOnlyList<PageDocument>, AeroError>(children);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load children for parent {ParentId}", parentId);
            return Prelude.Fail<IReadOnlyList<PageDocument>, AeroError>(
                AeroError.DatabaseError("Failed to load child pages."));
        }
    }

        /// <summary>
    /// GetAncestorsAsync method.
    /// </summary>
public async Task<Result<IReadOnlyList<PageDocument>, AeroError>> GetAncestorsAsync(
        long pageId, CancellationToken ct = default)
    {
        try
        {
            var siteId = _siteContext.SiteId;
            var page = await _session.LoadAsync<PageDocument>(pageId, ct);

            if (page is null || page.SiteId != siteId)
            {
                return Prelude.Fail<IReadOnlyList<PageDocument>, AeroError>(
                    AeroError.NotFoundError($"Page {pageId} not found."));
            }

            if (page.Depth == 0)
                return Prelude.Ok<IReadOnlyList<PageDocument>, AeroError>(Array.Empty<PageDocument>());

            // Build ancestor IDs from the materialized path
            // Path: "/sports/basketball/nba" → ancestors: "/sports", "/sports/basketball"
            var segments = page.Path.TrimStart('/').Split('/');
            var ancestorPaths = new List<string>(segments.Length - 1);
            for (var i = 1; i < segments.Length; i++)
            {
                ancestorPaths.Add("/" + string.Join("/", segments.Take(i)));
            }

            if (ancestorPaths.Count == 0)
                return Prelude.Ok<IReadOnlyList<PageDocument>, AeroError>(Array.Empty<PageDocument>());

            // Single query: load all ancestors by path
            var ancestors = await _session
                .Query<PageDocument>()
                .Where(x => x.SiteId == siteId && ancestorPaths.Contains(x.Path))
                .OrderBy(x => x.Depth)
                .ToListAsync(ct);

            return Prelude.Ok<IReadOnlyList<PageDocument>, AeroError>(ancestors);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load ancestors for page {PageId}", pageId);
            return Prelude.Fail<IReadOnlyList<PageDocument>, AeroError>(
                AeroError.DatabaseError("Failed to load ancestors."));
        }
    }

        /// <summary>
    /// MoveAsync method.
    /// </summary>
public async Task<Result<PageDocument, AeroError>> MoveAsync(
        long pageId, long? newParentId, int? order = null, CancellationToken ct = default)
    {
        try
        {
            var siteId = _siteContext.SiteId;

            // Load the page document directly (string-stream events appended separately)
            var page = await _session.LoadAsync<PageDocument>(pageId, ct);

            if (page is null || page.SiteId != siteId)
            {
                return Prelude.Fail<PageDocument, AeroError>(
                    AeroError.NotFoundError($"Page {pageId} not found."));
            }

            // Validate no circular reference
            if (newParentId.HasValue)
            {
                var parent = await _session.LoadAsync<PageDocument>(newParentId.Value, ct);
                if (parent is null || parent.SiteId != siteId)
                {
                    return Prelude.Fail<PageDocument, AeroError>(
                        AeroError.NotFoundError($"Parent page {newParentId} not found."));
                }

                if (parent.Path.StartsWith(page.Path + "/"))
                {
                    return Prelude.Fail<PageDocument, AeroError>(
                        AeroError.ConflictError("Cannot move a page under its own descendant."));
                }
            }

            var oldPath = page.Path;
            var oldParentId = page.ParentId;

            // Compute new path
            string newPath;
            int newDepth;

            if (newParentId.HasValue)
            {
                var parent = await _session.LoadAsync<PageDocument>(newParentId.Value, ct);
                newPath = parent!.Path.TrimEnd('/') + "/" + page.Slug;
                newDepth = parent.Depth + 1;
            }
            else
            {
                newPath = "/" + page.Slug;
                newDepth = 0;
            }

            page.ParentId = newParentId;
            page.Path = newPath;
            page.Depth = newDepth;
            page.Order = order ?? 0;
            page.ModifiedOn = DateTimeOffset.UtcNow;
            _session.Store(page);

            // Update descendant paths directly (derived fields, not historical events)
            if (oldPath != newPath)
            {
                await UpdateSlugReservationAsync(page, oldPath, ct);

                var descendants = await _session
                    .QueryAsync(new PagesByPathPrefixQuery
                    {
                        SiteId = siteId,
                        PathPrefix = oldPath + "/"
                    }, ct);

                foreach (var descendant in descendants)
                {
                    var oldDescendantPath = descendant.Path;
                    descendant.Path = newPath + descendant.Path[oldPath.Length..];
                    descendant.Depth = descendant.Path.Count(c => c == '/') - 1;
                    _session.Update(descendant);
                    await UpdateSlugReservationAsync(descendant, oldDescendantPath, ct);
                }
            }

            await _session.SaveChangesAsync(ct);

            // Publish path change via Wolverine outbox for alias/sitemap modules
            if (oldPath != newPath)
            {
                await _bus.PublishAsync(new PageSlugChanged(page.Id, oldPath, newPath));
            }

            _logger.LogInformation(
                "Moved page {PageId} from parent {OldParent} to {NewParent}, path {OldPath} → {NewPath}",
                pageId, oldParentId, newParentId, oldPath, newPath);

            return Prelude.Ok<PageDocument, AeroError>(page);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to move page {PageId}", pageId);
            return Prelude.Fail<PageDocument, AeroError>(
                AeroError.DatabaseError("Failed to move page."));
        }
    }

        /// <summary>
    /// ComputePathAsync method.
    /// </summary>
public async Task<Result<(string Path, int Depth), AeroError>> ComputePathAsync(
        long siteId, long? parentId, string slug,
        long? excludePageId = null,
        CancellationToken ct = default)
    {
        try
        {
            if (parentId is null or <= 0)
            {
                return Prelude.Ok<(string Path, int Depth), AeroError>(("/" + slug, 0));
            }

            var parent = await _session.LoadAsync<PageDocument>(parentId.Value, ct);
            if (parent is null || parent.SiteId != siteId)
            {
                return Prelude.Fail<(string Path, int Depth), AeroError>(
                    AeroError.NotFoundError($"Parent page {parentId} not found."));
            }

            var parentPath = string.IsNullOrEmpty(parent.Path) || parent.Path == "/"
                ? "/" + parent.Slug
                : parent.Path;
            var path = parentPath.TrimEnd('/') + "/" + slug;
            var depth = parent.Depth + 1;

            // Check uniqueness: no OTHER sibling with same slug
            var exists = await _session
                .Query<PageDocument>()
                .Where(x => x.SiteId == siteId
                    && x.ParentId == parentId
                    && x.Slug == slug
                    && x.Id != excludePageId
                    && x.Deleted == false)
                .AnyAsync(ct);

            if (exists)
            {
                return Prelude.Fail<(string Path, int Depth), AeroError>(
                    AeroError.ConflictError($"A page with slug '{slug}' already exists under parent {parentId}."));
            }

            return Prelude.Ok<(string Path, int Depth), AeroError>((path, depth));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compute path for slug {Slug} under parent {ParentId}", slug, parentId);
            return Prelude.Fail<(string Path, int Depth), AeroError>(
                AeroError.DatabaseError("Failed to compute page path."));
        }
    }

        /// <summary>
    /// GetNextSiblingOrderAsync method.
    /// </summary>
public async Task<Result<int, AeroError>> GetNextSiblingOrderAsync(
        long siteId, long? parentId, CancellationToken ct = default)
    {
        try
        {
            var maxOrder = await _session
                .Query<PageDocument>()
                .Where(x => x.SiteId == siteId && x.ParentId == parentId && !x.Deleted)
                .MaxAsync(x => x.Order, ct);

            return Prelude.Ok<int, AeroError>((int)maxOrder + 1);
        }
        catch (InvalidOperationException)
        {
            // No siblings exist — return 0
            return Prelude.Ok<int, AeroError>(0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get next sibling order for parent {ParentId}", parentId);
            return Prelude.Fail<int, AeroError>(
                AeroError.DatabaseError("Failed to compute sibling order."));
        }
    }

        /// <summary>
    /// UpdateDescendantPathsAsync method.
    /// </summary>
public async Task<Result<bool, AeroError>> UpdateDescendantPathsAsync(
        long pageId, string oldPath, string newPath, CancellationToken ct = default)
    {
        try
        {
            if (oldPath == newPath)
                return Prelude.Ok<bool, AeroError>(true);

            var siteId = _siteContext.SiteId;

            // Load the page itself to verify ownership
            var page = await _session.LoadAsync<PageDocument>(pageId, ct);
            if (page is null || page.SiteId != siteId)
            {
                return Prelude.Fail<bool, AeroError>(
                    AeroError.NotFoundError($"Page {pageId} not found."));
            }

            var descendants = await _session
                .QueryAsync(new PagesByPathPrefixQuery
                {
                    SiteId = siteId,
                    PathPrefix = oldPath + "/"
                }, ct);

            var depthDelta = newPath.Count(c => c == '/') - oldPath.Count(c => c == '/');

            foreach (var descendant in descendants)
            {
                var oldDescendantPath = descendant.Path;
                descendant.Path = newPath + descendant.Path[oldPath.Length..];
                descendant.Depth += depthDelta;
                _session.Update(descendant);
                await UpdateSlugReservationAsync(descendant, oldDescendantPath, ct);
            }

            await _session.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Updated {Count} descendant paths for page {PageId}: {OldPath} → {NewPath}",
                descendants.Count, pageId, oldPath, newPath);

            return Prelude.Ok<bool, AeroError>(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update descendant paths for page {PageId}", pageId);
            return Prelude.Fail<bool, AeroError>(
                AeroError.DatabaseError("Failed to update descendant paths."));
        }
    }

    private Task UpdateSlugReservationAsync(
        PageDocument page,
        string previousPath,
        CancellationToken cancellationToken)
        => ContentSlugReservation.ReserveAsync(
            _session,
            page.Id,
            ContentSlugOwnerType.Page,
            page.Path.TrimStart('/'),
            page.SiteId,
            page.Culture,
            previousPath.TrimStart('/'),
            cancellationToken);
}

/// <summary>
/// Raised when a page's slug or path changes. Handled by Alias + Sitemap modules via Wolverine.
/// </summary>
public sealed record PageSlugChanged(long PageId, string OldPath, string NewPath);

