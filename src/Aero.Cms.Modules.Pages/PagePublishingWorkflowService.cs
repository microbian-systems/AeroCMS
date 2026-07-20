using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Events;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Html;
using Aero.Core.Railway;
using Wolverine;

namespace Aero.Cms.Modules.Pages;

/// <summary>
/// Defines publication-state transitions for tracked page documents.
/// </summary>
/// <remarks>
/// Operations load pages by identifier and do not independently enforce authorization or
/// current-site ownership. Callers must authorize the transition and verify the page belongs
/// to the intended site before invoking this workflow.
/// </remarks>
public interface IPagePublishingWorkflowService
{
    /// <summary>
    /// Validates a draft and moves it into review.
    /// </summary>
    /// <param name="pageId">The page identifier.</param>
    /// <param name="ct">The token used for persistence and validation work.</param>
    /// <returns>A successful result when the draft is saved in review, or an error result.</returns>
    Task<Result<bool, AeroError>> SubmitForReviewAsync(long pageId, CancellationToken ct = default);

    /// <summary>
    /// Publishes a page that is currently in review.
    /// </summary>
    /// <param name="pageId">The page identifier.</param>
    /// <param name="reviewerId">The reviewer identifier written to the log.</param>
    /// <param name="notes">Optional review notes written to the log.</param>
    /// <param name="ct">The token used for persistence and validation work.</param>
    /// <returns>A successful result after the published snapshot is saved and notifications are sent, or an error result.</returns>
    Task<Result<bool, AeroError>> ApproveAsync(long pageId, string reviewerId, string? notes, CancellationToken ct = default);

    /// <summary>
    /// Returns a page in review to the draft state.
    /// </summary>
    /// <param name="pageId">The page identifier.</param>
    /// <param name="reviewerId">The reviewer identifier written to the log.</param>
    /// <param name="notes">Optional review notes written to the log.</param>
    /// <param name="ct">The token used for persistence.</param>
    /// <returns>A successful result after the draft state is saved, or an error result.</returns>
    Task<Result<bool, AeroError>> RejectAsync(long pageId, string reviewerId, string? notes, CancellationToken ct = default);

    /// <summary>
    /// Validates and immediately publishes a page without requiring the review state.
    /// </summary>
    /// <param name="pageId">The page identifier.</param>
    /// <param name="ct">The token used for persistence and validation work.</param>
    /// <returns>A successful result after the published snapshot is saved and notifications are sent, or an error result.</returns>
    Task<Result<bool, AeroError>> PublishNowAsync(long pageId, CancellationToken ct = default);

    /// <summary>
    /// Marks a page as archived and sends page-update notifications.
    /// </summary>
    /// <param name="pageId">The page identifier.</param>
    /// <param name="ct">The token used for persistence.</param>
    /// <returns>A successful result after the archived state is saved and notifications are sent, or an error result.</returns>
    Task<Result<bool, AeroError>> ArchiveAsync(long pageId, CancellationToken ct = default);
}

/// <summary>
/// Applies the tracked-document page lifecycle and publishes downstream update
/// notifications after publication or archival.
/// </summary>
/// <remarks>
/// Document persistence and message publication are sequential operations, not a
/// transaction coordinated by this service. A notification failure can therefore
/// be returned after the page state has already been saved. Public operations catch
/// cancellation exceptions raised in their bodies and translate them to database
/// error results. Pages are loaded by identifier without a current-site or authorization
/// check; those boundaries remain the caller's responsibility.
/// </remarks>
public sealed class PagePublishingWorkflowService(
    IDocumentSession session,
    IMessageBus bus,
    IHtmlContentValidator contentValidator,
    IStyleCompiler styleCompiler,
    ISiteStyleProfileResolver styleProfileResolver,
    ILogger<PagePublishingWorkflowService> logger) : IPagePublishingWorkflowService
{
    /// <inheritdoc />
    public async Task<Result<bool, AeroError>> SubmitForReviewAsync(long pageId, CancellationToken ct = default)
    {
        try
        {
            var page = await session.LoadAsync<PageDocument>(pageId, ct);
            if (page is null) return AeroError.NotFoundError($"Page {pageId} not found.");
            if (page.PublicationState != ContentPublicationState.Draft)
                return AeroError.ConflictError("Only draft pages can be submitted for review.");

            var validation = await ValidateDraftAsync(page, ct);
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

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
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
        var validation = await ValidateDraftAsync(page, ct);
        if (validation is Result<CompiledPageStyles>.Failure failure) return failure.Error;

        page.PublishDraftContent(DateTimeOffset.UtcNow);
        await SaveAsync(page, ct);
        await PublishNotificationsAsync(page);

        logger.LogInformation("Page {PageId} published at version {Version}", page.Id, page.PublishedVersion);
        return Prelude.Ok<bool, AeroError>(true);
    }

    private async Task<Result<CompiledPageStyles>> ValidateDraftAsync(
        PageDocument page,
        CancellationToken cancellationToken)
    {
        var contentValidation = contentValidator.Validate(page.DraftContent);
        if (contentValidation is Result<bool>.Failure failure)
            return failure.Error;

        var profileResult = await styleProfileResolver.ResolveAsync(
            page.SiteId,
            cancellationToken);
        if (profileResult is Result<IStyleProfile, AeroError>.Failure profileFailure)
            return profileFailure.Error;

        var profile = ((Result<IStyleProfile, AeroError>.Ok)profileResult).Value;
        return styleCompiler.Compile(page.DraftContent, profile);
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
