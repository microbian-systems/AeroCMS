using Aero.Cms.Abstractions.Events;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Data.Queries;
using Aero.Cms.Services;
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
        long pageId,
        long? newParentId,
        int? order = null,
        PreviousPathBehavior? previousPathBehavior = null,
        CancellationToken ct = default);

    /// <summary>
    /// Computes all historically published routes affected by a proposed slug or parent change.
    /// </summary>
    Task<Result<PageRouteChangeImpact, AeroError>> GetRouteChangeImpactAsync(
        long pageId,
        long? newParentId,
        string slug,
        CancellationToken ct = default);

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
    private readonly IPageRouteAliasWriter? _aliasWriter;

        /// <summary>
    /// Initializes a new instance of the <see cref="PageTreeService"/> class.
    /// </summary>
public PageTreeService(
        IDocumentSession session,
        ISiteContext siteContext,
        IMessageBus bus,
        ILogger<PageTreeService> logger,
        IPageRouteAliasWriter? aliasWriter = null)
    {
        _session = session;
        _siteContext = siteContext;
        _bus = bus;
        _logger = logger;
        _aliasWriter = aliasWriter;
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

    /// <inheritdoc />
    public async Task<Result<PageRouteChangeImpact, AeroError>> GetRouteChangeImpactAsync(
        long pageId,
        long? newParentId,
        string slug,
        CancellationToken ct = default)
    {
        try
        {
            var page = await _session.LoadAsync<PageDocument>(pageId, ct);
            if (page is null || page.SiteId != _siteContext.SiteId)
            {
                return Prelude.Fail<PageRouteChangeImpact, AeroError>(
                    AeroError.NotFoundError($"Page {pageId} not found."));
            }

            var pathResult = await ComputePathAsync(
                page.SiteId,
                newParentId,
                slug,
                page.Id,
                ct);
            if (pathResult is Result<(string Path, int Depth), AeroError>.Failure pathFailure)
            {
                return Prelude.Fail<PageRouteChangeImpact, AeroError>(pathFailure.Error);
            }

            var newPath = ((Result<(string Path, int Depth), AeroError>.Ok)pathResult).Value.Path;
            if (string.Equals(page.Path, newPath, StringComparison.Ordinal))
            {
                return Prelude.Ok<PageRouteChangeImpact, AeroError>(
                    new PageRouteChangeImpact(page.Id, page.Path, newPath, []));
            }

            var affected = new List<PageRouteChangeItem>();
            if (page.PublishedVersion > 0)
            {
                affected.Add(new PageRouteChangeItem(
                    page.Id,
                    page.Title,
                    page.Culture,
                    page.Path,
                    newPath));
            }

            var descendants = await _session.QueryAsync(
                new PagesByPathPrefixQuery
                {
                    SiteId = page.SiteId,
                    PathPrefix = page.Path.TrimEnd('/') + "/"
                },
                ct);

            foreach (var descendant in descendants.Where(x =>
                         x.PublishedVersion > 0
                         && string.Equals(x.Culture, page.Culture, StringComparison.OrdinalIgnoreCase)))
            {
                affected.Add(new PageRouteChangeItem(
                    descendant.Id,
                    descendant.Title,
                    descendant.Culture,
                    descendant.Path,
                    newPath + descendant.Path[page.Path.Length..]));
            }

            return Prelude.Ok<PageRouteChangeImpact, AeroError>(
                new PageRouteChangeImpact(page.Id, page.Path, newPath, affected));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compute route impact for page {PageId}", pageId);
            return Prelude.Fail<PageRouteChangeImpact, AeroError>(
                AeroError.DatabaseError("Failed to compute page route impact."));
        }
    }

        /// <summary>
    /// MoveAsync method.
    /// </summary>
public async Task<Result<PageDocument, AeroError>> MoveAsync(
        long pageId,
        long? newParentId,
        int? order = null,
        PreviousPathBehavior? previousPathBehavior = null,
        CancellationToken ct = default)
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

            var impactResult = await GetRouteChangeImpactAsync(pageId, newParentId, page.Slug, ct);
            if (impactResult is Result<PageRouteChangeImpact, AeroError>.Failure impactFailure)
            {
                return Prelude.Fail<PageDocument, AeroError>(impactFailure.Error);
            }

            var impact = ((Result<PageRouteChangeImpact, AeroError>.Ok)impactResult).Value;
            if (impact.RequiresDecision && previousPathBehavior is null)
            {
                return Prelude.Fail<PageDocument, AeroError>(
                    AeroError.ConflictError(
                        "This route has previously been published. Choose whether to preserve the old URL as a permanent redirect."));
            }

            var pathResult = await ComputePathAsync(siteId, newParentId, page.Slug, page.Id, ct);
            if (pathResult is Result<(string Path, int Depth), AeroError>.Failure pathFailure)
            {
                return Prelude.Fail<PageDocument, AeroError>(pathFailure.Error);
            }

            var (newPath, newDepth) =
                ((Result<(string Path, int Depth), AeroError>.Ok)pathResult).Value;

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

                foreach (var descendant in descendants.Where(x =>
                             string.Equals(x.Culture, page.Culture, StringComparison.OrdinalIgnoreCase)))
                {
                    var oldDescendantPath = descendant.Path;
                    descendant.Path = newPath + descendant.Path[oldPath.Length..];
                    descendant.Depth = descendant.Path.Count(c => c == '/') - 1;
                    _session.Update(descendant);
                    await UpdateSlugReservationAsync(descendant, oldDescendantPath, ct);
                }
            }

            PageRouteAliasStageResult? aliasStage = null;
            if (impact.RequiresDecision)
            {
                if (_aliasWriter is null)
                {
                    return Prelude.Fail<PageDocument, AeroError>(
                        AeroError.ConfigurationError("The page route alias writer is not configured."));
                }

                var aliasResult = await _aliasWriter.StageAsync(
                    _session,
                    impact.PreviouslyPublishedRoutes
                        .Select(item => new PageRouteAliasCandidate(
                            item.PageId,
                            siteId,
                            item.Culture,
                            item.OldPath,
                            item.NewPath,
                            previousPathBehavior == PreviousPathBehavior.CreatePermanentRedirect))
                        .ToList(),
                    ct);
                if (aliasResult is Result<PageRouteAliasStageResult, AeroError>.Failure aliasFailure)
                {
                    return Prelude.Fail<PageDocument, AeroError>(aliasFailure.Error);
                }

                aliasStage = ((Result<PageRouteAliasStageResult, AeroError>.Ok)aliasResult).Value;
            }

            await _session.SaveChangesAsync(ct);

            if (aliasStage?.HasChanges == true && _aliasWriter is not null)
            {
                await _aliasWriter.OnCommittedAsync(CancellationToken.None);
            }

            // Publish path change via Wolverine outbox for alias/sitemap modules
            if (oldPath != newPath)
            {
                await _bus.PublishAsync(new PageSlugChanged(page.Id, oldPath, newPath));
            }

            if (aliasStage?.HasChanges == true)
            {
                await _bus.PublishAsync(new PageRouteAliasesChangedEvent(
                    siteId,
                    page.Culture,
                    DateTimeOffset.UtcNow));
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

            foreach (var descendant in descendants.Where(x =>
                         string.Equals(x.Culture, page.Culture, StringComparison.OrdinalIgnoreCase)))
            {
                var oldDescendantPath = descendant.Path;
                descendant.Path = newPath + descendant.Path[oldPath.Length..];
                descendant.Depth += depthDelta;
                _session.Update(descendant);
                await UpdateSlugReservationAsync(descendant, oldDescendantPath, ct);
            }

            _logger.LogInformation(
                "Staged {Count} descendant path updates for page {PageId}: {OldPath} → {NewPath}",
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

