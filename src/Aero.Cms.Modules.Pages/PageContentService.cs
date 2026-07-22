
using Aero.Cms.Modules.Pages.Validators;
using Aero.Cms.Modules.Pages.Rendering;
using Aero.Core.Extensions;
using Wolverine;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Events;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Abstractions.Content.Composition;
using Aero.Cms.Abstractions.Pages.Composition;
using Aero.Cms.Abstractions.Requests;
using PageRouteChangeImpact = Aero.Cms.Abstractions.Http.Clients.PageRouteChangeImpact;
using Aero.Cms.Core.Entities;
using Aero.Cms.Html;
using Aero.Cms.Shared.Localization;
using Aero.Cms.Services;
using Aero.Core.Http;
using System.Globalization;
using System.Text.Json;
using ZiggyCreatures.Caching.Fusion;
using static Aero.Core.Railway.Prelude;


namespace Aero.Cms.Modules.Pages;

/// <summary>
/// Provides site-oriented page queries, draft mutations, culture forks, and deletion.
/// </summary>
public interface IPageContentService
{
    /// <summary>
    /// Loads a page by identifier, using the optional site-keyed cache.
    /// </summary>
    /// <param name="id">The page identifier.</param>
    /// <param name="cancellationToken">The token used for cache and store access.</param>
    /// <returns>The page, or a not-found/database error.</returns>
Task<Result<PageDocument?, AeroError>> LoadAsync(long id, CancellationToken cancellationToken = default);
    /// <summary>
    /// Finds a published page by path using the current UI culture and site-default fallback.
    /// </summary>
    /// <param name="slug">The slug or hierarchical path, optionally culture-prefixed.</param>
    /// <param name="cancellationToken">The token used for cache and store access.</param>
    /// <returns>The published page, or a not-found/database error.</returns>
Task<Result<PageDocument?, AeroError>> FindBySlugAsync(string slug, CancellationToken cancellationToken = default);
    /// <summary>
    /// Finds a published page by path in a requested culture, falling back to the site's default culture.
    /// </summary>
    /// <param name="slug">The slug or hierarchical path, optionally culture-prefixed.</param>
    /// <param name="culture">The requested culture; null uses the current UI culture.</param>
    /// <param name="cancellationToken">The token used for cache and store access.</param>
    /// <returns>The published page, or a not-found/database error.</returns>
Task<Result<PageDocument?, AeroError>> FindBySlugAsync(string slug, string? culture, CancellationToken cancellationToken = default);
    /// <summary>
    /// Loads the published page whose normalized path is the root path.
    /// </summary>
    /// <param name="cancellationToken">The token used for cache and store access.</param>
    /// <returns>The homepage, or a not-found/database error.</returns>
Task<Result<PageDocument?, AeroError>> LoadHomepageAsync(CancellationToken cancellationToken = default);
    /// <summary>
    /// Loads the published page whose normalized path is <c>/blog</c>.
    /// </summary>
    /// <param name="cancellationToken">The token used for cache and store access.</param>
    /// <returns>The blog listing page, or a not-found/database error.</returns>
Task<Result<PageDocument?, AeroError>> LoadBlogListingAsync(CancellationToken cancellationToken = default);
    /// <summary>
    /// Lists current-site pages ordered by title with optional title/slug filtering.
    /// </summary>
    /// <param name="skip">The number of matching records to skip.</param>
    /// <param name="take">The maximum number of records to return.</param>
    /// <param name="search">Optional case-insensitive title or slug substring.</param>
    /// <param name="cancellationToken">The token used for cache and store access.</param>
    /// <returns>The requested page and total matching count, or a database error.</returns>
Task<Result<(IReadOnlyList<PageDocument> Items, long TotalCount), AeroError>> GetAllPagesAsync(int skip = 0, int take = 10, string? search = null, CancellationToken cancellationToken = default);
    /// <summary>
    /// Lists non-deleted current-site variants in a translation group.
    /// </summary>
    /// <param name="TranslationGroupId">The translation-group identifier.</param>
    /// <param name="cancellationToken">The token used for the store query.</param>
    /// <returns>Variants ordered by culture, or a database error.</returns>
Task<Result<IReadOnlyList<PageDocument>, AeroError>> ListCultureVariantsAsync(long TranslationGroupId, CancellationToken cancellationToken = default);
    /// <summary>
    /// Creates a draft culture variant by cloning an existing page's draft.
    /// </summary>
    /// <param name="sourcePageId">The source page identifier.</param>
    /// <param name="targetCulture">A culture supported by the current site.</param>
    /// <param name="targetSlug">The new variant's slug.</param>
    /// <param name="cancellationToken">The token used for store and validation operations.</param>
    /// <returns>The saved draft variant, or a not-found, validation, conflict, or database error.</returns>
    /// <remarks>
    /// A matching parent-culture variant is used when available. Hierarchy path/order
    /// failures are ignored, leaving the fork's root defaults.
    /// </remarks>
Task<Result<PageDocument, AeroError>> ForkPageForCultureAsync(long sourcePageId, string targetCulture, string targetSlug, CancellationToken cancellationToken = default);
    /// <summary>
    /// Validates and saves draft content without publishing it.
    /// </summary>
    /// <param name="page">The page state and draft content to save.</param>
    /// <param name="cancellationToken">The token used through the document commit.</param>
    /// <returns>The persisted page, or a validation, not-found, or database error.</returns>
Task<Result<PageDocument, AeroError>> SaveAsync(PageDocument page, CancellationToken cancellationToken = default);
    /// <summary>
    /// Validates and saves draft content for an explicitly authorized site.
    /// </summary>
    /// <param name="page">The page state and draft content to save.</param>
    /// <param name="authorizedSiteId">The positive site identifier already authorized by the caller.</param>
    /// <param name="cancellationToken">The token used through the document commit.</param>
    /// <returns>The persisted page, or a validation, not-found, or database error.</returns>
Task<Result<PageDocument, AeroError>> SaveAsync(
        PageDocument page,
        long authorizedSiteId,
        CancellationToken cancellationToken = default);
    /// <summary>
    /// Creates a validated draft page and reserves its full route.
    /// </summary>
    /// <param name="request">The page creation request.</param>
    /// <param name="cancellationToken">The token used through the document commit.</param>
    /// <returns>The created draft page, or a validation/conflict/database error.</returns>
    /// <remarks>
    /// When a positive parent is supplied, hierarchy path/order results are applied
    /// only on success; hierarchy failures do not themselves stop creation.
    /// </remarks>
Task<Result<PageDocument, AeroError>> CreateAsync(CreatePageRequest request, CancellationToken cancellationToken = default);
    /// <summary>
    /// Updates page metadata and optional draft content, including descendant routes when needed.
    /// </summary>
    /// <param name="id">The page identifier.</param>
    /// <param name="request">The replacement metadata and optional draft payload.</param>
    /// <param name="cancellationToken">The token used through the document commit.</param>
    /// <returns>The updated page, or a not-found, validation, conflict, configuration, or database error.</returns>
Task<Result<PageDocument, AeroError>> UpdateAsync(long id, UpdatePageRequest request, CancellationToken cancellationToken = default);
    /// <summary>
    /// Deletes one page and its slug reservation.
    /// </summary>
    /// <param name="id">The page identifier.</param>
    /// <param name="cancellationToken">The token used through the document commit.</param>
    /// <returns>A successful result after deletion, or a not-found/database error.</returns>
Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken cancellationToken = default);
    /// <summary>
    /// Unpublishes one page or deletes it together with descendants.
    /// </summary>
    /// <param name="id">The page identifier.</param>
    /// <param name="deleteDescendants">
    /// False to unpublish only the selected page; true to delete it, its descendants,
    /// and their slug reservations.
    /// </param>
    /// <param name="cancellationToken">The token used through the document commit.</param>
    /// <returns>A successful result after persistence, or a not-found/database error.</returns>
Task<Result<bool, AeroError>> DeleteAsync(long id, bool deleteDescendants, CancellationToken cancellationToken = default);
    /// <summary>
    /// Bulk-deletes selected pages and, optionally, their descendants.
    /// </summary>
    /// <param name="ids">The selected page identifiers.</param>
    /// <param name="deleteDescendants">Whether to expand selections by materialized-path prefix.</param>
    /// <param name="cancellationToken">The token used through the document commit.</param>
    /// <returns>The number of distinct identifiers submitted for deletion, or a database error.</returns>
Task<Result<int, AeroError>> DeleteMultipleAsync(IReadOnlyList<long> ids, bool deleteDescendants, CancellationToken cancellationToken = default);
    /// <summary>
    /// Deletes every non-deleted current-site variant in a translation group.
    /// </summary>
    /// <param name="translationGroupId">The translation-group identifier.</param>
    /// <param name="cancellationToken">The token used through the document commit.</param>
    /// <returns>The number of variants submitted for deletion, or a database error.</returns>
Task<Result<int, AeroError>> DeleteTranslationGroupAsync(long translationGroupId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implements current-site page operations over a scoped Sable session.
/// </summary>
/// <param name="session">The scoped document session.</param>
/// <param name="bus">Publishes page notifications after successful commits.</param>
/// <param name="siteContext">The current site scope.</param>
/// <param name="logger">The page-content logger.</param>
/// <param name="contentValidator">Validates the draft HTML tree.</param>
/// <param name="styleCompiler">Validates draft style tokens by compiling them.</param>
/// <param name="styleProfileResolver">Resolves the site's style policy.</param>
/// <param name="actor">The audit actor, defaulting to <c>system</c>.</param>
/// <param name="cache">The optional read cache.</param>
/// <param name="pageTreeService">The optional hierarchy service required for route changes.</param>
/// <param name="aliasWriter">The optional alias writer required for previously published route changes.</param>
/// <remarks>
/// Write commits precede Wolverine publication and alias committed callbacks; those
/// side effects are not transactional with persistence here. This service does not
/// evict its optional read cache after writes. Identifier-based
/// <see cref="LoadAsync(long, CancellationToken)"/>
/// relies on the caller/session boundary and does not independently verify that the
/// loaded document belongs to the current site. Public methods catch
/// <see cref="Exception"/>, including cancellation exceptions raised inside their
/// bodies, and normally translate them to database-error results.
/// </remarks>
public sealed class AeroPageContentService(
    IDocumentSession session,
    IMessageBus bus,
    ISiteContext siteContext,
    ILogger<AeroPageContentService> logger,
    IHtmlContentValidator contentValidator,
    IStyleCompiler styleCompiler,
    ISiteStyleProfileResolver styleProfileResolver,
    string? actor = null,
    IFusionCache? cache = null,
    IPageTreeService? pageTreeService = null,
    IPageRouteAliasWriter? aliasWriter = null,
    IContentCompositionReferenceValidator? contentReferenceValidator = null,
    IPageRegisteredFragmentRegistry? registeredFragmentRegistry = null) : IPageContentService
{
    private const string PageCacheTag = "pages-list";
    private readonly ISiteContext _siteContext = siteContext;

    /// <inheritdoc />
public async Task<Result<PageDocument?, AeroError>> LoadAsync(long id, CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = BuildCacheKey($"id:{id}");
            var cached = await TryGetCacheAsync<PageDocument>(cacheKey, cancellationToken);
            if (cached is not null)
            {
                return cached.SiteId == _siteContext.SiteId
                    ? Prelude.Ok<PageDocument?, AeroError>(cached)
                    : Prelude.Fail<PageDocument?, AeroError>(
                        AeroError.NotFoundError($"Page with id '{id}' not found or access denied"));
            }

            var document = await session.LoadAsync<PageDocument>(id, cancellationToken);
            if (document is null || document.SiteId != _siteContext.SiteId)
            {
                return Prelude.Fail<PageDocument?, AeroError>(AeroError.NotFoundError($"Page with id '{id}' not found or access denied"));
            }

            await SetCacheAsync(cacheKey, document, cancellationToken);
            return Prelude.Ok<PageDocument?, AeroError>(document);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load page {PageId}", id);
            return Prelude.Fail<PageDocument?, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    /// <inheritdoc />
public Task<Result<PageDocument?, AeroError>> LoadHomepageAsync(CancellationToken cancellationToken = default)
        => FindBySlugAsync("/", cancellationToken);

    /// <inheritdoc />
public Task<Result<PageDocument?, AeroError>> LoadBlogListingAsync(CancellationToken cancellationToken = default)
        => FindBySlugAsync("blog", cancellationToken);

    /// <inheritdoc />
public async Task<Result<(IReadOnlyList<PageDocument> Items, long TotalCount), AeroError>> GetAllPagesAsync(int skip = 0, int take = 10, string? search = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = BuildCacheKey($"list:{skip}:{take}:{NormalizeCachePart(search)}");
            var cached = await TryGetCacheAsync<PageListCacheEntry>(cacheKey, cancellationToken);
            if (cached is not null)
            {
                return Prelude.Ok<(IReadOnlyList<PageDocument> Items, long TotalCount), AeroError>((cached.Items, cached.TotalCount));
            }

            var query = session.Query<PageDocument>().Where(x => x.SiteId == _siteContext.SiteId);

            IQueryable<PageDocument> filteredQuery = query;
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                filteredQuery = query.Where(x => x.Title.ToLower().Contains(s) || x.Slug.ToLower().Contains(s));
            }
            var stats = new global::AeroDB.Sable.QueryStatistics();
            var pages = await ((global::AeroDB.Sable.ISurrealDbQueryable<PageDocument>)filteredQuery)
                .OrderBy(x => x.Title)
                .Stats(out stats)
                .Skip(skip)
                .Take(take)
                .ToListAsync(cancellationToken);

            await SetCacheAsync(cacheKey, new PageListCacheEntry(pages.ToList(), stats.TotalResults), cancellationToken);
            return Prelude.Ok<(IReadOnlyList<PageDocument> Items, long TotalCount), AeroError>((pages, stats.TotalResults));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to query all pages (skip={Skip}, take={Take})", skip, take);
            return Prelude.Fail<(IReadOnlyList<PageDocument> Items, long TotalCount), AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    /// <inheritdoc />
public Task<Result<PageDocument?, AeroError>> FindBySlugAsync(string slug, CancellationToken cancellationToken = default)
        => FindBySlugAsync(slug, culture: null, cancellationToken);

    /// <inheritdoc />
public async Task<Result<PageDocument?, AeroError>> FindBySlugAsync(string slug, string? culture, CancellationToken cancellationToken = default)
    {
        try
        {
            var currentCulture = GetCurrentCulture(culture);
            var routeSlug = AeroCultureRoute.StripLeadingCulture(slug);
            var cacheKey = BuildCacheKey($"slug:{currentCulture}:{NormalizeCachePart(routeSlug)}");
            var cached = await TryGetCacheAsync<PageDocument>(cacheKey, cancellationToken);
            if (cached is not null)
            {
                return Prelude.Ok<PageDocument?, AeroError>(cached);
            }

            var normalized = ContentSlugDocument.Normalize(routeSlug);

            var pathToMatch = "/" + normalized;
            var directPage = await FindDirectPageAsync(pathToMatch, currentCulture, cancellationToken)
                ?? await FindDefaultCultureDirectPageAsync(pathToMatch, currentCulture, cancellationToken);
            if (directPage is not null)
            {
                await SetCacheAsync(cacheKey, directPage, cancellationToken);
                return Prelude.Ok<PageDocument?, AeroError>(directPage);
            }

            return Prelude.Fail<PageDocument?, AeroError>(AeroError.NotFoundError($"Page with slug '{routeSlug}' not found"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to find page by slug {Slug}", slug);
            return Prelude.Fail<PageDocument?, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    /// <inheritdoc />
public async Task<Result<IReadOnlyList<PageDocument>, AeroError>> ListCultureVariantsAsync(
        long TranslationGroupId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var variants = await session.Query<PageDocument>()
                .Where(x => x.SiteId == _siteContext.SiteId
                    && x.TranslationGroupId == TranslationGroupId
                    && x.Deleted == false)
                .OrderBy(x => x.Culture)
                .ToListAsync(cancellationToken);

            return Prelude.Ok<IReadOnlyList<PageDocument>, AeroError>(variants);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to list page culture variants for Translation Group {TranslationGroupId}", TranslationGroupId);
            return Prelude.Fail<IReadOnlyList<PageDocument>, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    /// <inheritdoc />
public async Task<Result<PageDocument, AeroError>> ForkPageForCultureAsync(
        long sourcePageId,
        string targetCulture,
        string targetSlug,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var source = await session.LoadAsync<PageDocument>(sourcePageId, cancellationToken);
            if (source is null || source.SiteId != _siteContext.SiteId)
            {
                return Prelude.Fail<PageDocument, AeroError>(
                    AeroError.NotFoundError($"Page with id '{sourcePageId}' not found or access denied"));
            }

            var normalizedCulture = ContentSlugDocument.NormalizeCulture(targetCulture);
            var supported = await IsSupportedCultureAsync(source.SiteId, normalizedCulture, cancellationToken);
            if (!supported)
            {
                return Prelude.Fail<PageDocument, AeroError>(
                    AeroError.ValidationError([$"Culture '{normalizedCulture}' is not supported by the current site."]));
            }

            var TranslationGroupId = source.TranslationGroupId ?? source.Id;
            var existingVariant = await session.Query<PageDocument>()
                .FirstOrDefaultAsync(x =>
                    x.SiteId == source.SiteId &&
                    x.TranslationGroupId == TranslationGroupId &&
                    x.Culture == normalizedCulture &&
                    x.Deleted == false,
                    cancellationToken);

            if (existingVariant is not null)
            {
                return Prelude.Fail<PageDocument, AeroError>(
                    AeroError.ConflictError($"Page already has a '{normalizedCulture}' variant."));
            }

            var fork = PageCultureForker.Fork(source, Snowflake.NewId(), normalizedCulture, targetSlug);
            if (source.ParentId is long sourceParentId)
            {
                var parentVariant = await FindParentCultureVariantAsync(sourceParentId, normalizedCulture, cancellationToken);
                if (parentVariant is not null)
                {
                    fork.ParentId = parentVariant.Id;
                    if (pageTreeService is not null)
                    {
                        var pathResult = await pageTreeService.ComputePathAsync(
                            source.SiteId,
                            parentVariant.Id,
                            fork.Slug,
                            ct: cancellationToken);

                        if (pathResult is Result<(string Path, int Depth), AeroError>.Ok pathOk)
                        {
                            fork.Path = pathOk.Value.Path;
                            fork.Depth = pathOk.Value.Depth;
                        }

                        var orderResult = await pageTreeService.GetNextSiblingOrderAsync(
                            source.SiteId,
                            parentVariant.Id,
                            cancellationToken);

                        if (orderResult is Result<int, AeroError>.Ok orderOk)
                        {
                            fork.Order = orderOk.Value;
                        }
                    }
                }
            }

            return await SaveAsync(fork, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fork page {PageId} to culture {Culture}", sourcePageId, targetCulture);
            return Prelude.Fail<PageDocument, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    /// <inheritdoc />
public async Task<Result<PageDocument, AeroError>> CreateAsync(CreatePageRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var slug = string.IsNullOrEmpty(request.Slug)
                ? request.Title.GenerateSlug()
                : request.Slug;

            var siteId = _siteContext.SiteId;
            var page = new PageDocument
            {
                Id = Snowflake.NewId(),
                SiteId = siteId,
                Culture = SitesModel.DefaultCultureName,
                Title = request.Title,
                Slug = slug,
                Summary = request.Summary,
                SeoTitle = request.SeoTitle,
                SeoDescription = request.SeoDescription,
                PublicationState = ContentPublicationState.Draft,
                ShowInNavMenu = request.ShowInNavMenu,
                ShowHeaderNavigation = request.ShowHeaderNavigation,
                HideFooter = request.HideFooter,
                ShowChatAgent = request.ShowChatAgent
            };

            var draftContentResult = DeserializeDraftContent(request.DraftContentJson);
            if (draftContentResult is Result<HtmlPageContent, AeroError>.Failure draftFailure)
            {
                return Prelude.Fail<PageDocument, AeroError>(draftFailure.Error);
            }

            var draftCompositionResult = DeserializeDraftComposition(request.DraftCompositionJson);
            if (draftCompositionResult is Result<PageCompositionDocument, AeroError>.Failure compositionFailure)
            {
                return Prelude.Fail<PageDocument, AeroError>(compositionFailure.Error);
            }

            var draftContent = ((Result<HtmlPageContent, AeroError>.Ok)draftContentResult).Value;
            var draftComposition =
                ((Result<PageCompositionDocument, AeroError>.Ok)draftCompositionResult).Value;
            page.DraftContent = draftContent;
            page.DraftComposition = draftComposition;

            page.TranslationGroupId = page.Id;

            // Compute hierarchy fields (Path, Depth, Order) BEFORE validation
            // so Path is available for both the validator and slug reservation.
            var parentId = request.ParentId;
            var path = "/" + page.Slug;
            var depth = 0;
            var order = 0;

            if (parentId is not null and > 0)
            {
                var parent = await session.LoadAsync<PageDocument>(parentId.Value, cancellationToken);
                if (parent is null || parent.SiteId != siteId || parent.Deleted)
                {
                    return Prelude.Fail<PageDocument, AeroError>(
                        AeroError.NotFoundError($"Parent page with id '{parentId}' not found or access denied"));
                }

                if (pageTreeService is null)
                {
                    return Prelude.Fail<PageDocument, AeroError>(
                        AeroError.DatabaseError("Page hierarchy service is required to create a child page."));
                }

                var pathResult = await pageTreeService.ComputePathAsync(siteId, parentId, page.Slug, ct: cancellationToken);
                if (pathResult is Result<(string Path, int Depth), AeroError>.Failure pathFailure)
                {
                    return Prelude.Fail<PageDocument, AeroError>(pathFailure.Error);
                }

                var pathOk = (Result<(string Path, int Depth), AeroError>.Ok)pathResult;
                path = pathOk.Value.Path;
                depth = pathOk.Value.Depth;

                var orderResult = await pageTreeService.GetNextSiblingOrderAsync(siteId, parentId, cancellationToken);
                if (orderResult is Result<int, AeroError>.Failure orderFailure)
                {
                    return Prelude.Fail<PageDocument, AeroError>(orderFailure.Error);
                }

                order = ((Result<int, AeroError>.Ok)orderResult).Value;
            }

            page.ParentId = parentId;
            page.Path = path;
            page.Depth = depth;
            page.Order = order;

            var validationResult = await ValidatePage(page);
            if (validationResult is Result<bool, AeroError>.Failure vf)
            {
                logger.LogWarning("Validation failed creating page '{Title}' (slug={Slug}): {Errors}", request.Title, request.Slug ?? "(auto)", vf.Error);
                return Prelude.Fail<PageDocument, AeroError>(vf.Error);
            }

            var htmlValidation = await ValidateHtmlDraftAsync(page, cancellationToken);
            if (htmlValidation is Result<bool, AeroError>.Failure htmlFailure)
            {
                return Prelude.Fail<PageDocument, AeroError>(htmlFailure.Error);
            }

            // Reserve slug for public URL routing — use the full Path for
            // hierarchical pages so /parent/child resolves correctly.
            var publicSlug = page.Path.TrimStart('/'); // "/about/team" → "about/team"
            await ContentSlugReservation.ReserveAsync(
                session,
                page.Id,
                ContentSlugOwnerType.Page,
                publicSlug,
                siteId,
                page.Culture,
                previousSlug: null,
                cancellationToken: cancellationToken);

            var now = DateTimeOffset.UtcNow;
            page.ReplaceDraftContent(draftContent, draftComposition, now);
            page.CreatedOn = now;
            page.CreatedBy = actor ?? "system";
            page.ModifiedBy = actor ?? "system";
            session.Store(page);
            await session.SaveChangesAsync(cancellationToken);

            // Publish events via Wolverine outbox
            await bus.PublishAsync(new PageViewModelCreated(
                page.ToViewModel(), $"Page created: {page.Title}"));
            await bus.PublishAsync(new PageContentUpdatedEvent(page.Id, page.SiteId, page.Slug, null));

            logger.LogInformation("Created page {PageId}: {Title} (slug={Slug})", page.Id, page.Title, page.Slug);
            return Prelude.Ok<PageDocument, AeroError>(page);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create page '{Title}' (slug={Slug})", request.Title, request.Slug ?? "(auto)");
            return Prelude.Fail<PageDocument, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    /// <inheritdoc />
public async Task<Result<PageDocument, AeroError>> UpdateAsync(long id, UpdatePageRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var page = await session.LoadAsync<PageDocument>(id, cancellationToken);

            if (page is null || page.SiteId != _siteContext.SiteId)
            {
                return Fail<PageDocument, AeroError>(
                    AeroError.NotFoundError($"Page with id '{id}' not found or access denied"));
            }

            var oldSlug = page.Slug;
            var oldPath = page.Path;
            var oldParentId = page.ParentId;
            PageRouteChangeImpact? routeImpact = null;
            var candidateDraftContent = page.DraftContent;
            var candidateDraftComposition = page.DraftComposition;
            var draftChanged = request.DraftContentJson is not null
                || request.DraftCompositionJson is not null;

            if (!string.Equals(oldSlug, request.Slug, StringComparison.Ordinal)
                || oldParentId != request.ParentId)
            {
                if (pageTreeService is null)
                {
                    return Prelude.Fail<PageDocument, AeroError>(
                        AeroError.DatabaseError("Page hierarchy service is required to update a page route."));
                }

                var impactResult = await pageTreeService.GetRouteChangeImpactAsync(
                    page.Id,
                    request.ParentId,
                    request.Slug,
                    cancellationToken);
                if (impactResult is Result<PageRouteChangeImpact, AeroError>.Failure impactFailure)
                {
                    return Prelude.Fail<PageDocument, AeroError>(impactFailure.Error);
                }

                routeImpact = ((Result<PageRouteChangeImpact, AeroError>.Ok)impactResult).Value;
                if (routeImpact.RequiresDecision && request.PreviousPathBehavior is null)
                {
                    return Prelude.Fail<PageDocument, AeroError>(
                        AeroError.ConflictError(
                            "This route has previously been published. Choose whether to preserve the old URL as a permanent redirect."));
                }
            }

            if (draftChanged)
            {
                var draftContentResult = request.DraftContentJson is null
                    ? Prelude.Ok<HtmlPageContent, AeroError>(page.DraftContent)
                    : DeserializeDraftContent(request.DraftContentJson);
                if (draftContentResult is Result<HtmlPageContent, AeroError>.Failure draftFailure)
                {
                    return Prelude.Fail<PageDocument, AeroError>(draftFailure.Error);
                }

                var draftCompositionResult = request.DraftCompositionJson is null
                    ? Prelude.Ok<PageCompositionDocument, AeroError>(page.DraftComposition)
                    : DeserializeDraftComposition(request.DraftCompositionJson);
                if (draftCompositionResult is Result<PageCompositionDocument, AeroError>.Failure compositionFailure)
                {
                    return Prelude.Fail<PageDocument, AeroError>(compositionFailure.Error);
                }

                candidateDraftContent =
                    ((Result<HtmlPageContent, AeroError>.Ok)draftContentResult).Value;
                candidateDraftComposition =
                    ((Result<PageCompositionDocument, AeroError>.Ok)draftCompositionResult).Value;
            }

            // Apply metadata update to the document
            ApplyUpdateRequest(page, request);

            if (!string.Equals(oldSlug, page.Slug, StringComparison.Ordinal)
                || oldParentId != page.ParentId)
            {
                if (pageTreeService is null)
                {
                    return Prelude.Fail<PageDocument, AeroError>(
                        AeroError.DatabaseError("Page hierarchy service is required to update a page route."));
                }

                var pathResult = await pageTreeService.ComputePathAsync(
                    page.SiteId,
                    page.ParentId,
                    page.Slug,
                    excludePageId: page.Id,
                    ct: cancellationToken);
                if (pathResult is Result<(string Path, int Depth), AeroError>.Failure pathFailure)
                {
                    return Prelude.Fail<PageDocument, AeroError>(pathFailure.Error);
                }

                var path = ((Result<(string Path, int Depth), AeroError>.Ok)pathResult).Value;
                page.Path = path.Path;
                page.Depth = path.Depth;
            }

            // Validate the updated page
            var validationResult = await ValidatePage(page);
            if (validationResult is Result<bool, AeroError>.Failure vf)
            {
                logger.LogWarning("Validation failed updating page {PageId}: {Errors}", id, vf.Error);
                return Prelude.Fail<PageDocument, AeroError>(vf.Error);
            }

            var htmlValidation = await ValidateHtmlDraftAsync(
                page.SiteId,
                page.Culture,
                candidateDraftContent,
                candidateDraftComposition,
                cancellationToken);
            if (htmlValidation is Result<bool, AeroError>.Failure htmlFailure)
            {
                return Prelude.Fail<PageDocument, AeroError>(htmlFailure.Error);
            }

            if (draftChanged)
            {
                page.ReplaceDraftContent(
                    candidateDraftContent,
                    candidateDraftComposition,
                    DateTimeOffset.UtcNow);
            }

            // Reserve the new slug path (if changed) — uses full Path so
            // hierarchical pages like /parent/child route correctly.
            if (!string.Equals(oldPath, page.Path, StringComparison.Ordinal))
            {
                var oldPublicSlug = oldPath.TrimStart('/');
                var newPublicSlug = page.Path.TrimStart('/');

                await ContentSlugReservation.ReserveAsync(
                    session,
                    id,
                    ContentSlugOwnerType.Page,
                    newPublicSlug,
                    _siteContext.SiteId,
                    page.Culture,
                    oldPublicSlug,
                    cancellationToken);
            }

            page.ModifiedOn = DateTimeOffset.UtcNow;
            page.ModifiedBy = actor ?? "system";
            session.Store(page);

            if (!string.Equals(oldPath, page.Path, StringComparison.Ordinal))
            {
                if (pageTreeService is not null)
                {
                    var descendantsResult = await pageTreeService.UpdateDescendantPathsAsync(
                        page.Id,
                        oldPath,
                        page.Path,
                        cancellationToken);
                    if (descendantsResult is Result<bool, AeroError>.Failure descendantsFailure)
                    {
                        return Prelude.Fail<PageDocument, AeroError>(descendantsFailure.Error);
                    }
                }
            }

            PageRouteAliasStageResult? aliasStage = null;
            if (routeImpact?.RequiresDecision == true)
            {
                if (aliasWriter is null)
                {
                    return Prelude.Fail<PageDocument, AeroError>(
                        AeroError.ConfigurationError("The page route alias writer is not configured."));
                }

                var aliasResult = await aliasWriter.StageAsync(
                    session,
                    routeImpact.PreviouslyPublishedRoutes
                        .Select(item => new PageRouteAliasCandidate(
                            item.PageId,
                            page.SiteId,
                            item.Culture,
                            item.OldPath,
                            item.NewPath,
                            request.PreviousPathBehavior == PreviousPathBehavior.CreatePermanentRedirect))
                        .ToList(),
                    cancellationToken);
                if (aliasResult is Result<PageRouteAliasStageResult, AeroError>.Failure aliasFailure)
                {
                    return Prelude.Fail<PageDocument, AeroError>(aliasFailure.Error);
                }

                aliasStage = ((Result<PageRouteAliasStageResult, AeroError>.Ok)aliasResult).Value;
            }

            await session.SaveChangesAsync(cancellationToken);

            if (aliasStage?.HasChanges == true && aliasWriter is not null)
            {
                await aliasWriter.OnCommittedAsync(CancellationToken.None);
            }

            // Publish events via Wolverine outbox
            await bus.PublishAsync(new PageViewModelUpdated(
                page.ToViewModel(), $"Page updated: {page.Title}"));

            if (page.PublicationState == ContentPublicationState.Published)
            {
                await bus.PublishAsync(new SlugUpdated(id, "Page", request.Slug, oldSlug));
            }

            await bus.PublishAsync(new PageContentUpdatedEvent(id, _siteContext.SiteId, request.Slug, oldSlug));

            if (aliasStage?.HasChanges == true)
            {
                await bus.PublishAsync(new PageRouteAliasesChangedEvent(
                    page.SiteId,
                    page.Culture,
                    DateTimeOffset.UtcNow));
            }

            logger.LogInformation("Updated page {PageId}: {Title} (slug={Slug})", id, page.Title, page.Slug);
            return Prelude.Ok<PageDocument, AeroError>(page);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update page {PageId}", id);
            return Prelude.Fail<PageDocument, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    /// <inheritdoc />
public async Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        try
        {
            var page = await session.LoadAsync<PageDocument>(id, cancellationToken);

            if (page is null || page.SiteId != _siteContext.SiteId)
                return Prelude.Fail<bool, AeroError>(AeroError.NotFoundError($"Page with id '{id}' not found or access denied"));

            var reservation = await session.Query<ContentSlugDocument>()
                .FirstOrDefaultAsync(x => x.OwnerId == id && x.OwnerType == ContentSlugOwnerType.Page && x.SiteId == _siteContext.SiteId, cancellationToken);

            if (reservation is not null)
            {
                session.Delete(reservation);
            }

            session.Delete(page);

            await session.SaveChangesAsync(cancellationToken);
            await bus.PublishAsync(new PageViewModelDeleted(
                page.ToViewModel(), $"Page deleted: {page.Title}"));
            await bus.PublishAsync(new PageContentUpdatedEvent(id, _siteContext.SiteId, page.Slug, page.Slug));

            logger.LogInformation("Deleted page {PageId}: {Slug}", id, page.Slug);
            return Prelude.Ok<bool, AeroError>(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete page {PageId}", id);
            return Prelude.Fail<bool, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    /// <inheritdoc />
public async Task<Result<bool, AeroError>> DeleteAsync(long id, bool deleteDescendants, CancellationToken cancellationToken = default)
    {
        try
        {
            var page = await session.LoadAsync<PageDocument>(id, cancellationToken);
            if (page is null || page.SiteId != _siteContext.SiteId)
                return Prelude.Fail<bool, AeroError>(AeroError.NotFoundError($"Page with id '{id}' not found or access denied"));

            if (!deleteDescendants)
            {
                // Unpublish the parent page — children remain as-is
                var previousState = page.PublicationState;
                page.UnpublishContent(DateTimeOffset.UtcNow);
                session.Store(page);
                await session.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Unpublished page {PageId} (was {PreviousState})", id, previousState);
                return Prelude.Ok<bool, AeroError>(true);
            }

            // Cascade delete: find all descendants by Path prefix using NgramIndex
            var prefix = page.Path == "/" ? "/" : page.Path.TrimEnd('/') + "/";
            var descendants = await session.Query<PageDocument>()
                .Where(x => x.SiteId == _siteContext.SiteId
                    && x.Path.StartsWith(prefix)
                    && x.Deleted == false)
                .ToListAsync(cancellationToken);

            // Exclude self — for root pages the prefix "/" matches everything
            descendants = descendants.Where(d => d.Id != page.Id).ToList();

            // Delete parent + descendants (deepest first)
            var toDelete = new List<PageDocument> { page };
            toDelete.AddRange(descendants.OrderByDescending(d => d.Path.Length));

            logger.LogInformation("Cascade-deleting page {PageId} ({Path}) with {Count} descendants",
                id, page.Path, descendants.Count);

            foreach (var doc in toDelete)
            {
                var reservation = await session.Query<ContentSlugDocument>()
                    .FirstOrDefaultAsync(x => x.OwnerId == doc.Id
                        && x.OwnerType == ContentSlugOwnerType.Page
                        && x.SiteId == _siteContext.SiteId, cancellationToken);
                if (reservation is not null)
                    session.Delete(reservation);

                session.Delete(doc);
            }

            await session.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Cascade-deleted page {PageId} and {DescendantCount} descendants", id, descendants.Count);
            return Prelude.Ok<bool, AeroError>(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete page {PageId}", id);
            return Prelude.Fail<bool, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    /// <inheritdoc />
public async Task<Result<int, AeroError>> DeleteMultipleAsync(IReadOnlyList<long> ids, bool deleteDescendants, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
            return Prelude.Ok<int, AeroError>(0);

        try
        {
            var requestedIds = ids.Distinct().ToList();
            var requestedPages = await session.Query<PageDocument>()
                .Where(x => x.SiteId == _siteContext.SiteId
                    && requestedIds.Contains(x.Id)
                    && x.Deleted == false)
                .ToListAsync(cancellationToken);

            // Validate the entire request before staging any deletion. This makes a
            // mixed-site or partially missing batch fail atomically and conceal which
            // identifier was outside the authorized site.
            if (requestedPages.Count != requestedIds.Count)
            {
                return Prelude.Fail<int, AeroError>(
                    AeroError.NotFoundError("One or more pages were not found or access was denied."));
            }

            var idList = requestedIds.ToList();

            // If cascade requested, expand the id list to include all descendants
            if (deleteDescendants)
            {
                foreach (var page in requestedPages)
                {
                    var prefix = page.Path == "/" ? "/" : page.Path.TrimEnd('/') + "/";
                    var descendants = await session.Query<PageDocument>()
                        .Where(x => x.SiteId == _siteContext.SiteId
                            && x.Path.StartsWith(prefix)
                            && x.Deleted == false)
                        .ToListAsync(cancellationToken);
                    idList.AddRange(descendants.Where(d => d.Id != page.Id).Select(d => d.Id));
                }

                idList = idList.Distinct().ToList();
            }

            // Bulk soft-delete documents via single SQL UPDATE (ISoftDeleted)
            session.DeleteWhere<PageDocument>(x =>
                x.SiteId == _siteContext.SiteId && idList.Contains(x.Id));

            // Bulk-clean slug reservations
            session.DeleteWhere<ContentSlugDocument>(x =>
                x.SiteId == _siteContext.SiteId
                && idList.Contains(x.OwnerId)
                && x.OwnerType == ContentSlugOwnerType.Page);

            await session.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Bulk-deleted {Count} pages (deleteDescendants={Cascade})", idList.Count, deleteDescendants);
            return Prelude.Ok<int, AeroError>(idList.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to bulk-delete {Count} pages", ids.Count);
            return Prelude.Fail<int, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    /// <inheritdoc />
public async Task<Result<int, AeroError>> DeleteTranslationGroupAsync(long translationGroupId, CancellationToken cancellationToken = default)
    {
        try
        {
            var variants = await session.Query<PageDocument>()
                .Where(x => x.SiteId == _siteContext.SiteId
                    && x.TranslationGroupId == translationGroupId
                    && x.Deleted == false)
                .ToListAsync(cancellationToken);

            if (variants.Count == 0)
            {
                return Prelude.Fail<int, AeroError>(
                    AeroError.NotFoundError(
                        $"Page translation group '{translationGroupId}' was not found or access was denied."));
            }

            var ids = variants.Select(x => x.Id).ToList();

            session.DeleteWhere<PageDocument>(x =>
                x.SiteId == _siteContext.SiteId && ids.Contains(x.Id));

            session.DeleteWhere<ContentSlugDocument>(x =>
                x.SiteId == _siteContext.SiteId
                && ids.Contains(x.OwnerId)
                && x.OwnerType == ContentSlugOwnerType.Page);

            await session.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Deleted page translation group {TranslationGroupId} with {Count} variants", translationGroupId, ids.Count);
            return Prelude.Ok<int, AeroError>(ids.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete page translation group {TranslationGroupId}", translationGroupId);
            return Prelude.Fail<int, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

/// <inheritdoc />
public Task<Result<PageDocument, AeroError>> SaveAsync(
        PageDocument page,
        CancellationToken cancellationToken = default)
        => SaveAsync(page, _siteContext.SiteId, cancellationToken);

    /// <inheritdoc />
public async Task<Result<PageDocument, AeroError>> SaveAsync(
        PageDocument page,
        long authorizedSiteId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(page);
            if (authorizedSiteId <= 0)
            {
                return Prelude.Fail<PageDocument, AeroError>(
                    AeroError.ValidationError(["The authorized site identifier must be positive."]));
            }

            if (page.SiteId == 0)
            {
                page.SiteId = authorizedSiteId;
            }
            else if (page.SiteId != authorizedSiteId)
            {
                return Prelude.Fail<PageDocument, AeroError>(
                    AeroError.NotFoundError($"Page with id '{page.Id}' not found or access denied"));
            }

            var validationResult = await ValidatePage(page);
            if (validationResult is Result<bool, AeroError>.Failure vf)
            {
                logger.LogWarning("Validation failed saving page {PageId}: {Errors}", page.Id, vf.Error);
                return Prelude.Fail<PageDocument, AeroError>(vf.Error);
            }

            var htmlValidation = await ValidateHtmlDraftAsync(page, cancellationToken);
            if (htmlValidation is Result<bool, AeroError>.Failure htmlFailure)
            {
                logger.LogWarning("HTML draft validation failed saving page {PageId}: {Errors}", page.Id, htmlFailure.Error);
                return Prelude.Fail<PageDocument, AeroError>(htmlFailure.Error);
            }

            var existingPage = await session.LoadAsync<PageDocument>(page.Id, cancellationToken);
            if (existingPage is not null && existingPage.SiteId != authorizedSiteId)
            {
                return Prelude.Fail<PageDocument, AeroError>(AeroError.NotFoundError($"Page with id '{page.Id}' not found or access denied"));
            }

            var targetPage = existingPage ?? page;
            var oldSlug = existingPage?.Slug;
            if (existingPage is not null && !ReferenceEquals(page, existingPage))
            {
                ApplyDraftMetadata(page, existingPage);
            }

            var now = DateTimeOffset.UtcNow;
            targetPage.ReplaceDraftContent(page.DraftContent, page.DraftComposition, now);

            targetPage.Culture = ContentSlugDocument.NormalizeCulture(targetPage.Culture);
            targetPage.TranslationGroupId ??= targetPage.Id;

            var targetPublicSlug = targetPage.Path.TrimStart('/');
            await ContentSlugReservation.ReserveAsync(
                session,
                targetPage.Id,
                ContentSlugOwnerType.Page,
                targetPublicSlug,
                authorizedSiteId,
                targetPage.Culture,
                oldSlug,  // oldSlug is the leaf; reservation handles full-path matching
                cancellationToken);

            var existingCreatedOn = existingPage?.CreatedOn;
            targetPage.CreatedOn = existingCreatedOn is null || existingCreatedOn == default ? now : existingCreatedOn.Value;
            targetPage.ModifiedBy = actor ?? "system";

            // Saving edits only the draft. Publication is an explicit workflow
            // operation that owns PublishedContent and publication versioning.
            if (existingPage is null)
            {
                targetPage.PublicationState = ContentPublicationState.Draft;
                targetPage.PublishedContent = null;
                targetPage.PublishedComposition = null;
                targetPage.PublishedOn = null;
                targetPage.PublishedVersion = 0;
            }

            session.Store(targetPage);
            await session.SaveChangesAsync(cancellationToken);

            // Publish rich event + keep lean events for existing subscribers
            var vm = targetPage.ToViewModel();

            if (existingPage is null)
                await bus.PublishAsync(new PageViewModelCreated(vm, $"Page saved: {targetPage.Title}"));
            else
                await bus.PublishAsync(new PageViewModelUpdated(vm, $"Page saved: {targetPage.Title}"));

            if (targetPage.PublicationState == ContentPublicationState.Published)
            {
                await bus.PublishAsync(new SlugUpdated(targetPage.Id, "Page", targetPage.Slug, oldSlug));
            }

            await bus.PublishAsync(new PageContentUpdatedEvent(targetPage.Id, targetPage.SiteId, targetPage.Slug, oldSlug));

            logger.LogInformation("Saved page {PageId}: {Title} (slug={Slug})", targetPage.Id, targetPage.Title, targetPage.Slug);
            return Prelude.Ok<PageDocument, AeroError>(targetPage);

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save page {PageId} (slug={Slug})", page.Id, page.Slug);
            return Prelude.Fail<PageDocument, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    private static async Task<Result<bool, AeroError>> ValidatePage(PageDocument page)
    {
        var validator = new PageDocumentValidator();
        var valid = await validator.ValidateAsync(page);

        if (valid.Errors.Any())
            return Prelude.Fail<bool, AeroError>(AeroError.ValidationError(valid.Errors.Select(e => e.ErrorMessage)));

        return Prelude.Ok<bool, AeroError>(true);
    }

    private async Task<Result<bool, AeroError>> ValidateHtmlDraftAsync(
        PageDocument page,
        CancellationToken cancellationToken) => await ValidateHtmlDraftAsync(
            page.SiteId,
            page.Culture,
            page.DraftContent,
            page.DraftComposition,
            cancellationToken);

    private async Task<Result<bool, AeroError>> ValidateHtmlDraftAsync(
        long siteId,
        string culture,
        HtmlPageContent draftContent,
        PageCompositionDocument draftComposition,
        CancellationToken cancellationToken)
    {
        var contentValidation = contentValidator.Validate(draftContent);
        if (contentValidation is Result<bool>.Failure contentFailure)
        {
            return Prelude.Fail<bool, AeroError>(contentFailure.Error);
        }

        var compositionValidation = await PageCompositionValidationPipeline.ValidateAsync(
            siteId,
            culture,
            draftContent,
            draftComposition,
            ContentReferenceValidationMode.Authoring,
            contentReferenceValidator,
            registeredFragmentRegistry,
            cancellationToken);
        if (compositionValidation is Result<bool, AeroError>.Failure compositionFailure)
        {
            return Prelude.Fail<bool, AeroError>(compositionFailure.Error);
        }

        var profileResult = await styleProfileResolver.ResolveAsync(
            siteId,
            cancellationToken);
        if (profileResult is Result<IStyleProfile, AeroError>.Failure profileFailure)
        {
            return Prelude.Fail<bool, AeroError>(profileFailure.Error);
        }

        var styleProfile = ((Result<IStyleProfile, AeroError>.Ok)profileResult).Value;
        var styleCompilation = styleCompiler.Compile(draftContent, styleProfile);
        return styleCompilation is Result<CompiledPageStyles>.Failure styleFailure
            ? Prelude.Fail<bool, AeroError>(styleFailure.Error)
            : Prelude.Ok<bool, AeroError>(true);
    }

    private static void ApplyUpdateRequest(PageDocument page, UpdatePageRequest request)
    {
        page.Title = request.Title;
        page.Slug = request.Slug;
        page.Summary = request.Summary;
        page.SeoTitle = request.SeoTitle;
        page.SeoDescription = request.SeoDescription;
        page.ShowInNavMenu = request.ShowInNavMenu;
        page.ShowHeaderNavigation = request.ShowHeaderNavigation;
        page.HideFooter = request.HideFooter;
        page.ShowChatAgent = request.ShowChatAgent;
        page.ParentId = request.ParentId;
    }

    private static Result<HtmlPageContent, AeroError> DeserializeDraftContent(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Prelude.Ok<HtmlPageContent, AeroError>(new HtmlPageContent());
        }

        try
        {
            var content = JsonSerializer.Deserialize(json, HtmlJsonContext.Default.HtmlPageContent);
            return content is null
                ? Prelude.Fail<HtmlPageContent, AeroError>(
                    AeroError.ValidationError(["The page draft content payload was empty."]))
                : Prelude.Ok<HtmlPageContent, AeroError>(content);
        }
        catch (JsonException exception)
        {
            return Prelude.Fail<HtmlPageContent, AeroError>(
                AeroError.ValidationError([$"The page draft content payload is invalid: {exception.Message}"]));
        }
    }

    private static Result<PageCompositionDocument, AeroError> DeserializeDraftComposition(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Prelude.Ok<PageCompositionDocument, AeroError>(new PageCompositionDocument());
        }

        try
        {
            var composition = JsonSerializer.Deserialize(
                json,
                PageCompositionJsonContext.Default.PageCompositionDocument);
            return composition is null
                ? Prelude.Fail<PageCompositionDocument, AeroError>(
                    AeroError.ValidationError(["The page draft composition payload was empty."]))
                : Prelude.Ok<PageCompositionDocument, AeroError>(composition);
        }
        catch (JsonException exception)
        {
            return Prelude.Fail<PageCompositionDocument, AeroError>(
                AeroError.ValidationError(
                    [$"The page draft composition payload is invalid: {exception.Message}"]));
        }
    }

    private static void ApplyDraftMetadata(PageDocument source, PageDocument target)
    {
        target.Kind = source.Kind;
        target.Slug = source.Slug;
        target.Title = source.Title;
        target.Summary = source.Summary;
        target.SeoTitle = source.SeoTitle;
        target.SeoDescription = source.SeoDescription;
        target.ShowInNavMenu = source.ShowInNavMenu;
        target.ShowHeaderNavigation = source.ShowHeaderNavigation;
        target.HeaderImageUrl = source.HeaderImageUrl;
        target.HideHeader = source.HideHeader;
        target.HideFooter = source.HideFooter;
        target.ShowChatAgent = source.ShowChatAgent;
    }

    private string BuildCacheKey(string suffix)
        => $"cms:page:{_siteContext.SiteId}:{suffix}";

    private async Task<PageDocument?> FindDirectPageAsync(
        string pathToMatch,
        string culture,
        CancellationToken cancellationToken)
    {
        var candidates = await session.Query<PageDocument>()
            .Where(candidate => candidate.SiteId == _siteContext.SiteId)
            .ToListAsync(cancellationToken);
        return candidates.FirstOrDefault(candidate =>
            candidate.PublicationState == ContentPublicationState.Published
            && !candidate.Deleted
            && string.Equals(candidate.Path, pathToMatch, StringComparison.OrdinalIgnoreCase)
            && string.Equals(candidate.Culture, culture, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<PageDocument?> FindDefaultCultureDirectPageAsync(
        string pathToMatch,
        string culture,
        CancellationToken cancellationToken)
    {
        var defaultCulture = await GetSiteDefaultCultureAsync(cancellationToken);
        if (string.Equals(culture, defaultCulture, StringComparison.OrdinalIgnoreCase))
            return null;

        return await FindDirectPageAsync(pathToMatch, defaultCulture, cancellationToken);
    }

    private async Task<string> GetSiteDefaultCultureAsync(CancellationToken cancellationToken)
    {
        var site = await session.LoadAsync<SitesModel>(_siteContext.SiteId, cancellationToken);
        var defaultCulture = site?.DefaultCulture ?? SitesModel.DefaultCultureName;

        return ContentSlugDocument.NormalizeCulture(defaultCulture);
    }

    private async Task<bool> IsSupportedCultureAsync(long siteId, string culture, CancellationToken cancellationToken)
    {
        var site = await session.LoadAsync<SitesModel>(siteId, cancellationToken);
        if (site is null)
        {
            return string.Equals(culture, SitesModel.DefaultCultureName, StringComparison.OrdinalIgnoreCase);
        }

        IReadOnlyList<string> supported = site.SupportedCultures is { Count: > 0 }
            ? site.SupportedCultures
            : [site.DefaultCulture ?? SitesModel.DefaultCultureName];

        return supported
            .Select(ContentSlugDocument.NormalizeCulture)
            .Any(x => string.Equals(x, culture, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<PageDocument?> FindParentCultureVariantAsync(
        long sourceParentId,
        string targetCulture,
        CancellationToken cancellationToken)
    {
        var sourceParent = await session.LoadAsync<PageDocument>(sourceParentId, cancellationToken);
        if (sourceParent is null || sourceParent.SiteId != _siteContext.SiteId || sourceParent.Deleted)
        {
            return null;
        }

        if (string.Equals(sourceParent.Culture, targetCulture, StringComparison.OrdinalIgnoreCase))
        {
            return sourceParent;
        }

        if (sourceParent.TranslationGroupId is null)
        {
            return null;
        }

        return await session.Query<PageDocument>()
            .FirstOrDefaultAsync(x =>
                x.SiteId == _siteContext.SiteId &&
                x.TranslationGroupId == sourceParent.TranslationGroupId &&
                x.Culture == targetCulture &&
                x.Deleted == false,
                cancellationToken);
    }

    private static string GetCurrentCulture(string? culture = null)
        => ContentSlugDocument.NormalizeCulture(culture ?? CultureInfo.CurrentUICulture.Name);

    private async Task<T?> TryGetCacheAsync<T>(string key, CancellationToken cancellationToken) where T : class
    {
        if (cache is null)
        {
            return null;
        }

        var cached = await cache.TryGetAsync<T>(key, token: cancellationToken);
        return cached.HasValue ? cached.Value : null;
    }

    private Task SetCacheAsync<T>(string key, T value, CancellationToken cancellationToken) where T : class
        => cache is null
            ? Task.CompletedTask
            : cache.SetAsync(key, value, tags: [PageCacheTag], token: cancellationToken).AsTask();

    private static string NormalizeCachePart(string? value)
        => string.IsNullOrWhiteSpace(value) ? "_" : value.Trim().Trim('/').ToLowerInvariant();

    private sealed record PageListCacheEntry(List<PageDocument> Items, long TotalCount);
}
