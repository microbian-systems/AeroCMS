using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Layout;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Events;
using Wolverine;

namespace Aero.Cms.Modules.Pages;

public interface IPagePublishingWorkflowService
{
    Task<Result<bool, AeroError>> SubmitForReviewAsync(long pageId, CancellationToken ct = default);
    Task<Result<bool, AeroError>> ApproveAsync(long pageId, string reviewerId, string? notes, CancellationToken ct = default);
    Task<Result<bool, AeroError>> RejectAsync(long pageId, string reviewerId, string? notes, CancellationToken ct = default);
    Task<Result<bool, AeroError>> PublishNowAsync(long pageId, CancellationToken ct = default);
    Task<Result<bool, AeroError>> ArchiveAsync(long pageId, CancellationToken ct = default);
}

public sealed class PagePublishingWorkflowService : IPagePublishingWorkflowService
{
    private readonly IDocumentSession _session;
    private readonly IMessageBus _bus;
    private readonly IPageLayoutManifestBuilder _layoutBuilder;
    private readonly ILogger<PagePublishingWorkflowService> _logger;

    public PagePublishingWorkflowService(
        IDocumentSession session,
        IMessageBus bus,
        IPageLayoutManifestBuilder layoutBuilder,
        ILogger<PagePublishingWorkflowService> logger)
    {
        _session = session;
        _bus = bus;
        _layoutBuilder = layoutBuilder;
        _logger = logger;
    }

    public async Task<Result<bool, AeroError>> SubmitForReviewAsync(long pageId, CancellationToken ct = default)
    {
        try
        {
            var page = await _session.LoadAsync<PageDocument>(pageId, ct);

            if (page is null)
                return AeroError.NotFoundError($"Page {pageId} not found.");

            if (page.PublicationState != ContentPublicationState.Draft)
                return AeroError.ConflictError("Only draft pages can be submitted for review.");

            _session.Events.Append($"page-{pageId}", new object[] { new PageStateChanged(ContentPublicationState.InReview) });
            await _session.SaveChangesAsync(ct);

            _logger.LogInformation("Page {PageId} submitted for review", pageId);
            return Prelude.Ok<bool, AeroError>(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to submit page {PageId} for review", pageId);
            return AeroError.DatabaseError("Failed to submit page for review.");
        }
    }

    public async Task<Result<bool, AeroError>> ApproveAsync(long pageId, string reviewerId, string? notes, CancellationToken ct = default)
    {
        try
        {
            var page = await _session.LoadAsync<PageDocument>(pageId, ct);

            if (page is null)
                return AeroError.NotFoundError($"Page {pageId} not found.");

            if (page.PublicationState != ContentPublicationState.InReview)
                return AeroError.ConflictError("Page must be in review to approve.");

            _session.Events.Append($"page-{pageId}", new object[] { new PageStateChanged(ContentPublicationState.Published) });
            await _session.SaveChangesAsync(ct);

            _logger.LogInformation("Page {PageId} approved by {Reviewer}", pageId, reviewerId);
            return Prelude.Ok<bool, AeroError>(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to approve page {PageId}", pageId);
            return AeroError.DatabaseError("Failed to approve page.");
        }
    }

    public async Task<Result<bool, AeroError>> RejectAsync(long pageId, string reviewerId, string? notes, CancellationToken ct = default)
    {
        try
        {
            var page = await _session.LoadAsync<PageDocument>(pageId, ct);

            if (page is null)
                return AeroError.NotFoundError($"Page {pageId} not found.");

            if (page.PublicationState != ContentPublicationState.InReview)
                return AeroError.ConflictError("Page must be in review to reject.");

            _session.Events.Append($"page-{pageId}", new object[] { new PageStateChanged(ContentPublicationState.Draft) });
            await _session.SaveChangesAsync(ct);

            _logger.LogInformation("Page {PageId} rejected by {Reviewer}", pageId, reviewerId);
            return Prelude.Ok<bool, AeroError>(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reject page {PageId}", pageId);
            return AeroError.DatabaseError("Failed to reject page.");
        }
    }

    public async Task<Result<bool, AeroError>> PublishNowAsync(long pageId, CancellationToken ct = default)
    {
        try
        {
            var page = await _session.LoadAsync<PageDocument>(pageId, ct);

            if (page is null)
                return AeroError.NotFoundError($"Page {pageId} not found.");

            var isCompositionPage = page.RootNodes is { Count: > 0 };

            // ── Build legacy layout manifest from editor state ───────
            List<LayoutRegion>? layoutRegions = null;
            var editorState = isCompositionPage
                ? null
                : await _session.LoadAsync<PageEditorState>(pageId, ct);
            if (!isCompositionPage && editorState?.Blocks is { Count: > 0 })
            {
                var blockIds = editorState.Blocks
                    .Where(p => p.BlockId.HasValue)
                    .Select(p => p.BlockId!.Value)
                    .Distinct()
                    .ToList();

                if (blockIds.Count > 0)
                {
                    var blocks = await _session.Query<BlockBase>()
                        .Where(b => blockIds.Contains(b.Id))
                        .ToListAsync(ct);
                    var blockDict = blocks
                        .Where(b => b is not null)
                        .ToDictionary(b => b.Id);

                    var regions = await _layoutBuilder.BuildAsync(editorState, blockDict, ct);
                    layoutRegions = regions.ToList();
                }
            }

            // ── Emit publish events ──────────────────────────────────
            var version = page.PublishedVersion + 1;
            var stateChanged = new PageStateChanged(ContentPublicationState.Published);

            PageCompositionPublished? compositionPublished = null;
            PagePublished? legacyPublished = null;

            if (isCompositionPage)
            {
                compositionPublished = new PageCompositionPublished(
                    PageId: pageId,
                    SiteId: page.SiteId,
                    PublishedCompositionId: Snowflake.NewId(),
                    PublishedVersion: version,
                    Culture: page.Culture,
                    Title: page.Title,
                    Slug: page.Slug,
                    Summary: page.Summary,
                    SeoTitle: page.SeoTitle,
                    SeoDescription: page.SeoDescription,
                    RootNodes: page.RootNodes,
                    LayoutRegions: null,
                    Kind: page.Kind,
                    ShowHeaderNavigation: page.ShowHeaderNavigation,
                    HeaderImageUrl: page.HeaderImageUrl,
                    HideHeader: page.HideHeader,
                    HideFooter: page.HideFooter,
                    ShowChatAgent: page.ShowChatAgent,
                    BlockIdMap: page.BlockIdMap);

                _session.Events.Append($"page-{pageId}",
                    new object[] { compositionPublished });
            }
            else
            {
                legacyPublished = new PagePublished(PageId: pageId, Version: version, LayoutRegions: layoutRegions);

                _session.Events.Append($"page-{pageId}",
                    new object[] { legacyPublished });
            }

            _session.Events.Append($"page-{pageId}",
                new object[] { stateChanged });
            await _session.SaveChangesAsync(ct);

            if (compositionPublished is not null)
            {
                page.Apply(compositionPublished);
            }
            else if (legacyPublished is not null)
            {
                page.Apply(legacyPublished);
            }
            page.Apply(stateChanged);

            // Broadcast for cache eviction
            await _bus.PublishAsync(new PageViewModelUpdated(
                page.ToViewModel(), $"Page published: {page.Title}"));
            await _bus.PublishAsync(new PageContentUpdatedEvent(pageId, page.SiteId, page.Slug, page.Slug));

            _logger.LogInformation("Page {PageId} published (version {Version}, {RegionCount} layout regions)",
                pageId, version, layoutRegions?.Count ?? 0);
            return Prelude.Ok<bool, AeroError>(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish page {PageId}", pageId);
            return AeroError.DatabaseError("Failed to publish page.");
        }
    }

    public async Task<Result<bool, AeroError>> ArchiveAsync(long pageId, CancellationToken ct = default)
    {
        try
        {
            var page = await _session.LoadAsync<PageDocument>(pageId, ct);

            if (page is null)
                return AeroError.NotFoundError($"Page {pageId} not found.");

            var stateChanged = new PageStateChanged(ContentPublicationState.Archived);
            _session.Events.Append($"page-{pageId}", new object[] { stateChanged });
            await _session.SaveChangesAsync(ct);

            page.Apply(stateChanged);

            // Broadcast for cache eviction
            await _bus.PublishAsync(new PageViewModelUpdated(
                page.ToViewModel(), $"Page archived: {page.Title}"));
            await _bus.PublishAsync(new PageContentUpdatedEvent(pageId, page.SiteId, page.Slug, page.Slug));

            _logger.LogInformation("Page {PageId} archived", pageId);
            return Prelude.Ok<bool, AeroError>(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to archive page {PageId}", pageId);
            return AeroError.DatabaseError("Failed to archive page.");
        }
    }
}
