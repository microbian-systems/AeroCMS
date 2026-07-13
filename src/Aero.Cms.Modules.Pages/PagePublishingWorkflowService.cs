using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Events;
using Aero.Cms.Html;
using Aero.Core.Railway;
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

/// <summary>
/// Applies the tracked-document page lifecycle without a page-content event stream.
/// </summary>
public sealed class PagePublishingWorkflowService(
    IDocumentSession session,
    IMessageBus bus,
    IHtmlContentValidator contentValidator,
    IStyleCompiler styleCompiler,
    IStyleProfile styleProfile,
    ILogger<PagePublishingWorkflowService> logger) : IPagePublishingWorkflowService
{
    public async Task<Result<bool, AeroError>> SubmitForReviewAsync(long pageId, CancellationToken ct = default)
    {
        try
        {
            var page = await session.LoadAsync<PageDocument>(pageId, ct);
            if (page is null) return AeroError.NotFoundError($"Page {pageId} not found.");
            if (page.PublicationState != ContentPublicationState.Draft)
                return AeroError.ConflictError("Only draft pages can be submitted for review.");

            var validation = ValidateDraft(page);
            if (validation is Result<CompiledPageStyles>.Failure failure) return failure.Error;

            page.PublicationState = ContentPublicationState.InReview;
            page.ModifiedOn = DateTimeOffset.UtcNow;
            await SaveAsync(page, ct);

            logger.LogInformation("Page {PageId} submitted for review", pageId);
            return Prelude.Ok<bool, AeroError>(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to submit page {PageId} for review", pageId);
            return AeroError.DatabaseError("Failed to submit page for review.");
        }
    }

    public async Task<Result<bool, AeroError>> ApproveAsync(
        long pageId,
        string reviewerId,
        string? notes,
        CancellationToken ct = default)
    {
        try
        {
            var page = await session.LoadAsync<PageDocument>(pageId, ct);
            if (page is null) return AeroError.NotFoundError($"Page {pageId} not found.");
            if (page.PublicationState != ContentPublicationState.InReview)
                return AeroError.ConflictError("Page must be in review to approve.");

            var result = await PublishAsync(page, ct);
            if (result.IsSuccess)
            {
                logger.LogInformation(
                    "Page {PageId} approved by {Reviewer}; notes: {Notes}",
                    pageId,
                    reviewerId,
                    notes);
            }

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to approve page {PageId}", pageId);
            return AeroError.DatabaseError("Failed to approve page.");
        }
    }

    public async Task<Result<bool, AeroError>> RejectAsync(
        long pageId,
        string reviewerId,
        string? notes,
        CancellationToken ct = default)
    {
        try
        {
            var page = await session.LoadAsync<PageDocument>(pageId, ct);
            if (page is null) return AeroError.NotFoundError($"Page {pageId} not found.");
            if (page.PublicationState != ContentPublicationState.InReview)
                return AeroError.ConflictError("Page must be in review to reject.");

            page.PublicationState = ContentPublicationState.Draft;
            page.ModifiedOn = DateTimeOffset.UtcNow;
            await SaveAsync(page, ct);

            logger.LogInformation(
                "Page {PageId} rejected by {Reviewer}; notes: {Notes}",
                pageId,
                reviewerId,
                notes);
            return Prelude.Ok<bool, AeroError>(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to reject page {PageId}", pageId);
            return AeroError.DatabaseError("Failed to reject page.");
        }
    }

    public async Task<Result<bool, AeroError>> PublishNowAsync(long pageId, CancellationToken ct = default)
    {
        try
        {
            var page = await session.LoadAsync<PageDocument>(pageId, ct);
            return page is null
                ? AeroError.NotFoundError($"Page {pageId} not found.")
                : await PublishAsync(page, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to publish page {PageId}", pageId);
            return AeroError.DatabaseError("Failed to publish page.");
        }
    }

    public async Task<Result<bool, AeroError>> ArchiveAsync(long pageId, CancellationToken ct = default)
    {
        try
        {
            var page = await session.LoadAsync<PageDocument>(pageId, ct);
            if (page is null) return AeroError.NotFoundError($"Page {pageId} not found.");

            page.PublicationState = ContentPublicationState.Archived;
            page.ModifiedOn = DateTimeOffset.UtcNow;
            await SaveAsync(page, ct);
            await PublishNotificationsAsync(page);

            logger.LogInformation("Page {PageId} archived", pageId);
            return Prelude.Ok<bool, AeroError>(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to archive page {PageId}", pageId);
            return AeroError.DatabaseError("Failed to archive page.");
        }
    }

    private async Task<Result<bool, AeroError>> PublishAsync(PageDocument page, CancellationToken ct)
    {
        var validation = ValidateDraft(page);
        if (validation is Result<CompiledPageStyles>.Failure failure) return failure.Error;

        page.PublishDraftContent(DateTimeOffset.UtcNow);
        await SaveAsync(page, ct);
        await PublishNotificationsAsync(page);

        logger.LogInformation("Page {PageId} published at version {Version}", page.Id, page.PublishedVersion);
        return Prelude.Ok<bool, AeroError>(true);
    }

    private Result<CompiledPageStyles> ValidateDraft(PageDocument page)
    {
        var contentValidation = contentValidator.Validate(page.DraftContent);
        return contentValidation is Result<bool>.Failure failure
            ? failure.Error
            : styleCompiler.Compile(page.DraftContent, styleProfile);
    }

    private async Task SaveAsync(PageDocument page, CancellationToken ct)
    {
        session.Store(page);
        await session.SaveChangesAsync(ct);
    }

    private async Task PublishNotificationsAsync(PageDocument page)
    {
        await bus.PublishAsync(new PageViewModelUpdated(
            page.ToViewModel(), $"Page state changed: {page.Title}"));
        await bus.PublishAsync(new PageContentUpdatedEvent(page.Id, page.SiteId, page.Slug, page.Slug));
    }
}
