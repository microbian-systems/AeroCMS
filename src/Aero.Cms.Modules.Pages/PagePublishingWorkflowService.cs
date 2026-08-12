using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Events;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Abstractions.Content.Composition;
using Aero.Cms.Abstractions.Content.Views;
using Aero.Cms.Abstractions.Pages.Composition;
using Aero.Cms.Abstractions.Pages.Rendering;
using Aero.Cms.Html;
using Aero.Cms.Modules.Pages.Validators;
using Aero.Cms.Modules.Pages.Rendering;
using Aero.Core.Railway;
using System.Collections.Immutable;
using Wolverine;

namespace Aero.Cms.Modules.Pages;

/// <summary>
/// Defines publication-state transitions for tracked page documents.
/// </summary>
/// <remarks>
/// Administrative publication uses the overload carrying an authorized site identifier,
/// so ownership is checked in the same session that performs the mutation.
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
    /// Validates and immediately publishes a page only when it belongs to the authorized site.
    /// </summary>
    Task<Result<bool, AeroError>> PublishNowAsync(
        long pageId,
        long authorizedSiteId,
        CancellationToken ct = default);

    /// <summary>Publishes using an explicit server-authoritative tenant/site scope.</summary>
    Task<Result<bool, AeroError>> PublishNowAsync(
        long pageId,
        long authorizedTenantId,
        long authorizedSiteId,
        CancellationToken ct = default);

    /// <summary>
    /// Validates and atomically publishes a batch of pages owned by the authorized site.
    /// </summary>
    /// <remarks>
    /// Every draft is validated before any page is mutated. All page snapshots are then
    /// committed in one unit of work before downstream notifications are published.
    /// </remarks>
    Task<Result<bool, AeroError>> PublishBatchAsync(
        IReadOnlyCollection<long> pageIds,
        long authorizedSiteId,
        CancellationToken ct = default);

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
/// error results. The explicit-site publication overload is the administrative boundary;
/// unscoped lifecycle methods remain available for seed and internal workflows.
/// </remarks>
public sealed class PagePublishingWorkflowService(
    IDocumentSession session,
    IMessageBus bus,
    IHtmlContentValidator contentValidator,
    IStyleCompiler styleCompiler,
    ISiteStyleProfileResolver styleProfileResolver,
    ILogger<PagePublishingWorkflowService> logger,
    IContentCompositionReferenceValidator? contentReferenceValidator = null,
    IPageRegisteredFragmentRegistry? registeredFragmentRegistry = null,
    IPageRendererRegistry? pageRendererRegistry = null,
    IPageSourceVersionStore? pageSourceVersionStore = null,
    IPageContentQueryResolver? pageContentQueryResolver = null,
    IPageRouteTemplateValidator? routeTemplateValidator = null) : IPagePublishingWorkflowService
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
    public async Task<Result<bool, AeroError>> PublishNowAsync(
        long pageId,
        long authorizedSiteId,
        CancellationToken ct = default)
        => await PublishNowAsync(pageId, 0, authorizedSiteId, ct);

    /// <inheritdoc />
    public async Task<Result<bool, AeroError>> PublishNowAsync(
        long pageId,
        long authorizedTenantId,
        long authorizedSiteId,
        CancellationToken ct = default)
    {
        try
        {
            var page = await session.LoadAsync<PageDocument>(pageId, ct);
            return page is null || page.SiteId != authorizedSiteId
                ? AeroError.NotFoundError($"Page {pageId} not found.")
                : await PublishAsync(page, new ContentViewScope(authorizedTenantId, authorizedSiteId), ct);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to publish page {PageId} for authorized site {SiteId}",
                pageId,
                authorizedSiteId);
            return AeroError.DatabaseError("Failed to publish page.");
        }
    }

    /// <inheritdoc />
    public async Task<Result<bool, AeroError>> PublishBatchAsync(
        IReadOnlyCollection<long> pageIds,
        long authorizedSiteId,
        CancellationToken ct = default)
    {
        try
        {
            if (pageIds is null || pageIds.Count == 0)
            {
                return AeroError.ValidationError(
                    ["At least one page identifier is required for batch publication."]);
            }

            if (pageIds.Any(pageId => pageId <= 0))
            {
                return AeroError.ValidationError(
                    ["Page identifiers must be positive."]);
            }

            var distinctPageIds = pageIds.Distinct().ToArray();
            var loadedPages = await session.LoadManyAsync<PageDocument>(distinctPageIds, ct);
            var pagesById = loadedPages.ToDictionary(page => page.Id);
            if (distinctPageIds.Any(pageId =>
                    !pagesById.TryGetValue(pageId, out var page)
                    || page.SiteId != authorizedSiteId))
            {
                return AeroError.NotFoundError(
                    "One or more pages were not found for the authorized site.");
            }

            var pages = distinctPageIds
                .Select(pageId => pagesById[pageId])
                .ToArray();

            foreach (var page in pages)
            {
                var validation = await ValidateDraftAsync(page, ct);
                if (validation is Result<CompiledPageStyles>.Failure failure)
                {
                    return failure.Error;
                }
            }

            var publishedOn = DateTimeOffset.UtcNow;
            foreach (var page in pages)
            {
                page.PublishDraftContent(publishedOn);
            }

            session.Store(pages);
            await session.SaveChangesAsync(ct);

            foreach (var page in pages)
            {
                await PublishNotificationsAsync(page);
            }

            logger.LogInformation(
                "Published {PageCount} pages for authorized site {SiteId}",
                pages.Length,
                authorizedSiteId);
            return Prelude.Ok<bool, AeroError>(true);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to publish page batch for authorized site {SiteId}",
                authorizedSiteId);
            return AeroError.DatabaseError("Failed to publish page batch.");
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
        => await PublishAsync(page, new ContentViewScope(0, page.SiteId), ct);

    private async Task<Result<bool, AeroError>> PublishAsync(
        PageDocument page,
        ContentViewScope scope,
        CancellationToken ct)
    {
        var validation = await ValidateDraftAsync(page, scope, ct);
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
        => await ValidateDraftAsync(page, new ContentViewScope(0, page.SiteId), cancellationToken);

    private async Task<Result<CompiledPageStyles>> ValidateDraftAsync(
        PageDocument page,
        ContentViewScope scope,
        CancellationToken cancellationToken)
    {
        if (routeTemplateValidator is not null)
        {
            var routeValidation = await routeTemplateValidator.ValidateDraftAsync(page, cancellationToken);
            if (routeValidation is Result<bool, AeroError>.Failure routeFailure)
                return routeFailure.Error;
        }

        var bindingValidation = PageRouteTemplate.ValidateCompositionBindings(
            page.DraftRouteTemplate,
            page.DraftComposition);
        if (bindingValidation is Result<bool, AeroError>.Failure bindingFailure)
            return bindingFailure.Error;

        var contentValidation = contentValidator.Validate(page.DraftContent);
        if (contentValidation is Result<bool>.Failure failure)
            return failure.Error;

        var compositionValidation = await PageCompositionValidationPipeline.ValidateAsync(
            scope,
            page.Culture,
            page.DraftContent,
            page.DraftComposition,
            ContentReferenceValidationMode.Publishing,
            contentReferenceValidator,
            registeredFragmentRegistry,
            cancellationToken);
        if (compositionValidation is Result<bool, AeroError>.Failure compositionFailure)
            return compositionFailure.Error;

        var profileResult = await styleProfileResolver.ResolveAsync(
            page.SiteId,
            cancellationToken);
        if (profileResult is Result<IStyleProfile, AeroError>.Failure profileFailure)
            return profileFailure.Error;

        var profile = ((Result<IStyleProfile, AeroError>.Ok)profileResult).Value;
        var compiled = styleCompiler.Compile(page.DraftContent, profile);
        if (compiled is Result<CompiledPageStyles>.Failure)
        {
            return compiled;
        }

        var rendererValidation = await ValidateSourceRendererAsync(
            page,
            cancellationToken);
        return rendererValidation is Result<bool>.Failure rendererFailure
            ? rendererFailure.Error
            : compiled;
    }

    private async Task<Result<bool>> ValidateSourceRendererAsync(
        PageDocument page,
        CancellationToken cancellationToken)
    {
        var rendererId = PageRendererIds.NormalizeOrDefault(page.RendererId);
        if (pageRendererRegistry is null)
        {
            return AeroError.ConfigurationError(
                "The page renderer registry is not configured.");
        }

        var rendererResult = pageRendererRegistry.Resolve(rendererId);
        if (rendererResult is Result<IPageRenderer>.Failure rendererFailure)
        {
            return rendererFailure.Error;
        }

        var renderer = ((Result<IPageRenderer>.Ok)rendererResult).Value;
        if (!renderer.Descriptor.RequiresSource)
        {
            return true;
        }

        if (pageSourceVersionStore is null
            || pageContentQueryResolver is null)
        {
            return AeroError.ConfigurationError(
                $"{renderer.Descriptor.DisplayName} page publication dependencies are not configured.");
        }

        var sourceResult = await pageSourceVersionStore.LoadAsync(
            page.DraftSourceVersionId,
            page.SiteId,
            page.Id,
            rendererId,
            cancellationToken);
        if (sourceResult is Result<PageSourceVersionSnapshot?>.Failure sourceFailure)
        {
            return sourceFailure.Error;
        }

        if (((Result<PageSourceVersionSnapshot?>.Ok)sourceResult).Value is not { } source)
        {
            return AeroError.NotFoundError(
                "Page source version not found or access denied.");
        }

        var queryResult = await pageContentQueryResolver.ResolveAsync(
            page.SiteId,
            page.Culture,
            page.DraftComposition.ContentQueries,
            includeDrafts: false,
            cancellationToken);
        if (queryResult is Result<PageContentQueryResolution>.Failure queryFailure)
        {
            return queryFailure.Error;
        }

        var rendered = await renderer.RenderAsync(
            new PageRenderRequest(
                new PageRenderMetadata(
                    page.Id,
                    page.SiteId,
                    rendererId,
                    page.Title,
                    page.Slug,
                    page.Path,
                    page.Culture),
                new PageRenderSource(
                    source.Id,
                    source.RendererId,
                    source.Source,
                    source.SourceHash),
                page.DraftContent,
                page.DraftComposition,
                ImmutableDictionary<long, int>.Empty,
                ((Result<PageContentQueryResolution>.Ok)queryResult).Value,
                IsPreview: false),
            cancellationToken);
        return rendered is Result<RenderedPage>.Failure renderFailure
            ? renderFailure.Error
            : true;
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
