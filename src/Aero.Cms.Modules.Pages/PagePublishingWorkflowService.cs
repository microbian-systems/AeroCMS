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

            _session.Events.Append($"page-{pageId}", new PageStateChanged(ContentPublicationState.InReview));
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

            _session.Events.Append($"page-{pageId}", new PageStateChanged(ContentPublicationState.Published));
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

            _session.Events.Append($"page-{pageId}", new PageStateChanged(ContentPublicationState.Draft));
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

            // ── Build layout manifest from editor state ──────────────
            List<LayoutRegion>? layoutRegions = null;
            var editorState = await _session.LoadAsync<PageEditorState>(pageId, ct);
            if (editorState?.Blocks is { Count: > 0 })
            {
                var blockIds = editorState.Blocks
                    .Where(p => p.BlockId.HasValue)
                    .Select(p => p.BlockId!.Value)
                    .Distinct()
                    .ToList();

                if (blockIds.Count > 0)
                {
                    var blocks = await _session.Query<BlockBase>()
                        .Where(b => b.Id.IsOneOf(blockIds.ToArray()))
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
            _session.Events.Append($"page-{pageId}",
                new PagePublished(PageId: pageId, Version: version, LayoutRegions: layoutRegions));
            _session.Events.Append($"page-{pageId}",
                new PageStateChanged(ContentPublicationState.Published));
            await _session.SaveChangesAsync(ct);

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

            _session.Events.Append($"page-{pageId}", new PageStateChanged(ContentPublicationState.Archived));
            await _session.SaveChangesAsync(ct);

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
