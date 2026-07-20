using System.Text.Encodings.Web;
using Aero.Cms.Abstractions.Ai;
using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Http;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Abstractions.Requests;
using Aero.Cms.Html;
using Aero.Core.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using GrainCreateRequest = Aero.Cms.Abstractions.Requests.CreatePageRequest;
using GrainUpdateRequest = Aero.Cms.Abstractions.Requests.UpdatePageRequest;

namespace Aero.Cms.Modules.Pages.Areas.Api.v1;

/// <summary>
/// Thin admin API for page management — delegates tracked-document persistence
/// to <see cref="IAeroPageActor"/> (Orleans grain). Tree/navigation delegates to
/// existing services (IPageTreeService, INavigationService).
/// </summary>
/// <remarks>
/// Administrative and preview endpoints require the matching permission for the
/// site selected in the manager context.
/// </remarks>
public static class PagesApi
{
    /// <summary>
    /// Maps page CRUD, translation, route-impact, publication, bulk deletion, and
    /// HTML preview endpoints under the configured admin API prefix.
    /// </summary>
    /// <param name="app">The endpoint route builder to extend.</param>
public static void MapPagesApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/{HttpConstants.ApiPrefix}admin/pages")
            .WithTags("Admin - Pages");
        
        group.MapGet("/", ListPages)
            .WithName("ListPages")
            .RequireAuthorization("site:read");
        
        group.MapGet("/{id:long}", GetPageById)
            .WithName("GetPageById")
            .RequireAuthorization("site:read");
        
        group.MapGet("/slug/{*slug}", GetPageBySlug)
            .WithName("GetPageBySlug")
            .RequireAuthorization("site:read");
        
        group.MapGet("/drafts/{id:long}", PreviewDraftPage)
            .WithName("PreviewDraftPage")
            .RequireAuthorization("site:read");
        
        group.MapPost("/", CreatePage)
            .WithName("CreatePage")
            .RequireAuthorization("site:create");

        group.MapGet("/{id:long}/translations", ListPageTranslations)
            .WithName("ListPageTranslations")
            .RequireAuthorization("site:read");

        group.MapPost("/{id:long}/translations", ForkPageToCulture)
            .WithName("ForkPageToCulture")
            .RequireAuthorization("site:create");

        group.MapPost("/{id:long}/ai-translate", TranslatePageWithAi)
            .WithName("TranslatePageWithAi")
            .RequireAuthorization("site:update");
        
        group.MapPut("/{id:long}", UpdatePage)
            .WithName("UpdatePage")
            .RequireAuthorization("site:update");

        group.MapPost("/{id:long}/route-impact", GetRouteChangeImpact)
            .WithName("GetPageRouteChangeImpact")
            .RequireAuthorization("site:read");
        
        group.MapDelete("/{id:long}", DeletePage)
            .WithName("DeletePage")
            .RequireAuthorization("site:delete");

        group.MapDelete("/translation-groups/{translationGroupId:long}", DeleteTranslationGroup)
            .WithName("DeletePageTranslationGroup")
            .RequireAuthorization("site:delete");

        group.MapPut("/translation-groups/{translationGroupId:long}/publish", PublishTranslationGroup)
            .WithName("PublishPageTranslationGroup")
            .RequireAuthorization("site:update");

        group.MapPut("/translation-groups/{translationGroupId:long}/unpublish", UnpublishTranslationGroup)
            .WithName("UnpublishPageTranslationGroup")
            .RequireAuthorization("site:update");
        
        group.MapDelete("/{id:long}/cascade", DeletePageCascade)
            .WithName("DeletePageCascade")
            .RequireAuthorization("site:delete");
        
        group.MapPost("/delete-multiple", DeleteMultiplePages)
            .WithName("DeleteMultiplePages")
            .RequireAuthorization("site:delete");
        
        group.MapPut("/{id:long}/publish", PublishPage)
            .WithName("PublishPage")
            .RequireAuthorization("site:update");
        
        group.MapPut("/{id:long}/unpublish", UnpublishPage)
            .WithName("UnpublishPage")
            .RequireAuthorization("site:update");

        // Preview endpoints (moved from Headless PreviewApi)
        app.MapGet($"/{HttpConstants.ApiPrefix}admin/preview/pages/{{id:long}}", PreviewPage)
            .WithName("PreviewPage")
            .WithTags("Admin - Preview")
            .RequireAuthorization("site:read");

        app.MapPost($"/{HttpConstants.ApiPrefix}admin/preview/pages/render-fragment", PreviewPageFragment)
            .WithName("PreviewPageFragment")
            .WithTags("Admin - Preview")
            .RequireAuthorization("site:update");
    }

    // ── Grain-backed handlers ─────────────────────────────────────────

    private static async Task<IResult> ListPages(
        [FromServices] IAeroPageActor pagesActor,
        [FromServices] ILoggerFactory loggerFactory,
        [FromServices] ISiteContext siteContext,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(PagesApi));
        try
        {
            var (items, totalCount) = await pagesActor.GetAllPagesAsync(siteContext.SiteId, skip, take, search, ct);
            var summary = items.Select(p => new PageSummary(
                p.Id, p.Title, p.Slug,
                p.CreatedOn.DateTime,
                p.PublishedOn?.DateTime,
                p.Summary)).ToList();

            return TypedResults.Ok(new PagedResult<PageSummary>(summary, totalCount, skip, take));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing pages");
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> GetPageById(
        long id,
        [FromServices] IAeroPageActor pagesActor,
        [FromServices] ISiteContext siteContext,
        CancellationToken ct)
    {
        var result = await pagesActor.GetByIdAsync(id, siteContext.SiteId, ct);
        return !string.IsNullOrWhiteSpace(result.error.Message)
            ? TypedResults.NotFound(result.error)
            : TypedResults.Ok(MapToDetail(result.data));
    }

    private static async Task<IResult> GetPageBySlug(
        string slug,
        [FromServices] IAeroPageActor pagesActor,
        [FromServices] ISiteContext siteContext,
        CancellationToken ct)
    {
        var result = await pagesActor.GetBySlugAsync(siteContext.SiteId, slug, ct);
        return !string.IsNullOrWhiteSpace(result.error.Message)
            ? TypedResults.NotFound(result.error)
            : TypedResults.Ok(MapToDetail(result.data));
    }

    private static async Task<IResult> PreviewDraftPage(
        long id,
        [FromServices] IAeroPageActor pagesActor,
        [FromServices] ISiteContext siteContext,
        [FromQuery] long? previewVersion = null,
        CancellationToken ct = default)
    {
        var result = await pagesActor.GetByIdAsync(id, siteContext.SiteId, ct);
        if (!string.IsNullOrWhiteSpace(result.error.Message))
            return TypedResults.NotFound(result.error);

        var url = previewVersion is { } version
            ? $"/_cms/preview/pages/drafts/{id}?previewVersion={version}"
            : $"/_cms/preview/pages/drafts/{id}";
        return TypedResults.Redirect(url);
    }

    private static async Task<IResult> CreatePage(
        [FromBody] Aero.Cms.Abstractions.Http.Clients.CreatePageRequest request,
        [FromServices] IAeroPageActor pagesActor,
        [FromServices] ILoggerFactory loggerFactory,
        [FromServices] ISiteContext siteContext,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(typeof(PagesApi));
        try
        {
            if (request.ParentId is > 0)
            {
                var parent = await pagesActor.GetByIdAsync(
                    request.ParentId.Value,
                    siteContext.SiteId,
                    ct);
                if (!string.IsNullOrWhiteSpace(parent.error.Message))
                    return TypedResults.NotFound(parent.error);
            }

            var grainRequest = new GrainCreateRequest(
                request.Title,
                request.Slug,
                request.Summary,
                request.SeoTitle,
                request.SeoDescription,
                request.PublicationState,
                request.ParentId,
                request.ShowInNavMenu,
                request.ShowHeaderNavigation,
                request.HideFooter,
                request.ShowChatAgent,
                siteContext.SiteId,
                DraftContentJson: SerializeDraftContent(request.DraftContent));

            var result = await pagesActor.CreateAsync(grainRequest, ct);
            return !string.IsNullOrWhiteSpace(result.error.Message)
                ? TypedResults.BadRequest(new ProblemDetails
                {
                    Title = "Failed to create page",
                    Detail = result.error.Message,
                    Status = StatusCodes.Status400BadRequest
                })
                : TypedResults.Created($"/{HttpConstants.ApiPrefix}admin/pages/{result.data.Id}", MapToDetail(result.data));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating page");
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> UpdatePage(
        long id,
        [FromBody] Aero.Cms.Abstractions.Http.Clients.UpdatePageRequest request,
        [FromServices] IAeroPageActor pagesActor,
        [FromServices] ILoggerFactory loggerFactory,
        [FromServices] ISiteContext siteContext,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(typeof(PagesApi));
        try
        {
            var grainRequest = new GrainUpdateRequest(
                id,
                request.Title,
                request.Slug,
                request.Summary,
                request.SeoTitle,
                request.SeoDescription,
                request.PublicationState,
                request.ParentId,
                request.ShowInNavMenu,
                request.ShowHeaderNavigation,
                request.HideFooter,
                request.ShowChatAgent,
                DraftContentJson: SerializeDraftContent(request.DraftContent),
                PreviousPathBehavior: request.PreviousPathBehavior);

            var existing = await pagesActor.GetByIdAsync(id, siteContext.SiteId, ct);
            if (!string.IsNullOrWhiteSpace(existing.error.Message))
                return TypedResults.NotFound(existing.error);

            if (request.ParentId is > 0)
            {
                var parent = await pagesActor.GetByIdAsync(
                    request.ParentId.Value,
                    siteContext.SiteId,
                    ct);
                if (!string.IsNullOrWhiteSpace(parent.error.Message))
                    return TypedResults.NotFound(parent.error);
            }

            var result = await pagesActor.UpdateAsync(grainRequest, siteContext.SiteId, ct);
            return !string.IsNullOrWhiteSpace(result.error.Message)
                ? TypedResults.BadRequest(new ProblemDetails
                {
                    Title = "Failed to update page",
                    Detail = result.error.Message,
                    Status = StatusCodes.Status400BadRequest
                })
                : TypedResults.Ok(MapToDetail(result.data));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating page {Id}", id);
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> GetRouteChangeImpact(
        long id,
        [FromBody] PageRouteChangeRequest request,
        [FromServices] IAeroPageActor pagesActor,
        [FromServices] ISiteContext siteContext,
        CancellationToken ct)
    {
        var existing = await pagesActor.GetByIdAsync(id, siteContext.SiteId, ct);
        if (!string.IsNullOrWhiteSpace(existing.error.Message))
            return TypedResults.NotFound(existing.error);

        var impact = await pagesActor.GetRouteChangeImpactAsync(
            id,
            siteContext.SiteId,
            request.Slug,
            request.ParentId,
            ct);
        return string.IsNullOrWhiteSpace(impact.ErrorMessage)
            ? TypedResults.Ok(impact)
            : TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Failed to calculate page route impact",
                Detail = impact.ErrorMessage,
                Status = StatusCodes.Status400BadRequest
            });
    }

    private static async Task<IResult> ListPageTranslations(
        long id,
        [FromServices] IAeroPageActor pagesActor,
        [FromServices] ILoggerFactory loggerFactory,
        [FromServices] ISiteContext siteContext,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(typeof(PagesApi));
        try
        {
            var existing = await pagesActor.GetByIdAsync(id, siteContext.SiteId, ct);
            if (!string.IsNullOrWhiteSpace(existing.error.Message))
                return TypedResults.NotFound(existing.error);

            var variants = await pagesActor.ListCultureVariantsAsync(id, siteContext.SiteId, ct);
            return TypedResults.Ok(variants.Select(MapToDetail).ToList());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing page translations for page {Id}", id);
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> ForkPageToCulture(
        long id,
        [FromBody] ForkPageCultureRequest request,
        [FromServices] IAeroPageActor pagesActor,
        [FromServices] ILoggerFactory loggerFactory,
        [FromServices] ISiteContext siteContext,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(typeof(PagesApi));
        try
        {
            var existing = await pagesActor.GetByIdAsync(id, siteContext.SiteId, ct);
            if (!string.IsNullOrWhiteSpace(existing.error.Message))
                return TypedResults.NotFound(existing.error);

            var result = await pagesActor.ForkPageForCultureAsync(
                id,
                siteContext.SiteId,
                request.Culture,
                request.Slug,
                ct);
            return !string.IsNullOrWhiteSpace(result.error.Message)
                ? TypedResults.BadRequest(new ProblemDetails
                {
                    Title = "Failed to create page translation",
                    Detail = result.error.Message,
                    Status = StatusCodes.Status400BadRequest
                })
                : TypedResults.Created($"/{HttpConstants.ApiPrefix}admin/pages/{result.data.Id}", MapToDetail(result.data));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating page translation for page {Id}", id);
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> TranslatePageWithAi(
        long id,
        [FromBody] AiTranslatePageRequest request,
        [FromServices] IPageContentService pageService,
        [FromServices] IQuerySession query,
        [FromServices] IAiContentTranslationService translationService,
        CancellationToken ct = default)
    {
        if (request.Targets.Count == 0)
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "No target cultures",
                Detail = "At least one target culture is required."
            });
        }

        var sourceResult = await pageService.LoadAsync(id, ct);
        if (sourceResult is not Result<PageDocument?, AeroError>.Ok { Value: not null } sourceOk)
        {
            return TypedResults.NotFound(new { error = "Source page was not found." });
        }

        var source = sourceOk.Value;
        var site = await query.LoadAsync<SitesModel>(source.SiteId, ct);
        var supportedCultures = GetSupportedCultures(site);
        var groupId = source.TranslationGroupId ?? source.Id;
        var variantsResult = await pageService.ListCultureVariantsAsync(groupId, ct);
        var variants = variantsResult is Result<IReadOnlyList<PageDocument>, AeroError>.Ok variantsOk
            ? variantsOk.Value
            : [source];

        var immediateResults = new List<AiTranslatePageCultureResult>();
        var plans = new List<AiTranslatePagePlan>();
        var plannedCultures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var target in request.Targets)
        {
            var culture = ContentSlugDocument.NormalizeCulture(target.Culture);
            if (!plannedCultures.Add(culture))
            {
                continue;
            }

            if (CultureEquals(culture, source.Culture))
            {
                immediateResults.Add(FailedPageTranslation(culture, "Target culture must be different from the source culture."));
                continue;
            }

            if (!supportedCultures.Contains(culture))
            {
                immediateResults.Add(FailedPageTranslation(culture, $"Culture '{culture}' is not supported by this site."));
                continue;
            }

            var existing = variants.FirstOrDefault(x => CultureEquals(x.Culture, culture));
            if (existing is not null && !request.OverwriteExisting)
            {
                immediateResults.Add(FailedPageTranslation(culture, $"A '{culture}' translation already exists."));
                continue;
            }

            var slug = string.IsNullOrWhiteSpace(target.Slug)
                ? BuildDefaultLocalizedSlug(source.Slug, culture)
                : target.Slug.Trim().Trim('/');

            plans.Add(new AiTranslatePagePlan(culture, slug, existing));
        }

        var translatedPlans = await Task.WhenAll(plans.Select(plan =>
            TranslatePagePlanAsync(source, plan, request.ProviderId, translationService, ct)));

        var results = new List<AiTranslatePageCultureResult>(immediateResults);
        foreach (var translated in translatedPlans)
        {
            if (!translated.Succeeded || translated.Response is null)
            {
                results.Add(FailedPageTranslation(translated.Culture, translated.Error ?? "AI translation failed."));
                continue;
            }

            var saveResult = await SaveTranslatedPageAsync(
                source.Id,
                translated.Plan,
                translated.Response,
                pageService,
                ct);

            results.Add(saveResult);
        }

        return TypedResults.Ok(new AiTranslatePageResult(results
            .OrderBy(x => x.Culture, StringComparer.OrdinalIgnoreCase)
            .ToList()));
    }

    private static async Task<IResult> DeletePage(
        long id,
        [FromServices] IAeroPageActor pagesActor,
        [FromServices] ISiteContext siteContext,
        CancellationToken ct)
    {
        var result = await pagesActor.DeleteAsync(new DeletePageRequest(id), siteContext.SiteId, ct);
        return !string.IsNullOrWhiteSpace(result.error.Message)
            ? TypedResults.NotFound(result.error)
            : TypedResults.Ok(true);
    }

    private static async Task<IResult> DeleteTranslationGroup(
        long translationGroupId,
        [FromServices] IPageContentService pageService,
        CancellationToken ct)
    {
        var result = await pageService.DeleteTranslationGroupAsync(translationGroupId, ct);
        return result switch
        {
            Result<int, AeroError>.Ok ok => TypedResults.Ok(new DeleteMultipleResult(ok.Value)),
            Result<int, AeroError>.Failure { Error: AeroError.NotFound } =>
                TypedResults.NotFound(new ProblemDetails
                {
                    Title = "Page translation group not found",
                    Detail = "The requested translation group was not found.",
                    Status = StatusCodes.Status404NotFound
                }),
            Result<int, AeroError>.Failure failure => TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Failed to delete translation group",
                Detail = failure.Error.ToString(),
                Status = StatusCodes.Status400BadRequest
            }),
            _ => TypedResults.Problem("Unknown delete result.")
        };
    }

    private static Task<IResult> PublishTranslationGroup(
        long translationGroupId,
        [FromServices] IPageContentService pageService,
        CancellationToken ct)
    {
        return SetPageTranslationGroupPublicationStateAsync(
            translationGroupId,
            ContentPublicationState.Published,
            pageService,
            ct);
    }

    private static Task<IResult> UnpublishTranslationGroup(
        long translationGroupId,
        [FromServices] IPageContentService pageService,
        CancellationToken ct)
    {
        return SetPageTranslationGroupPublicationStateAsync(
            translationGroupId,
            ContentPublicationState.Draft,
            pageService,
            ct);
    }

    private static async Task<IResult> DeletePageCascade(
        long id,
        [FromServices] IAeroPageActor pagesActor,
        [FromServices] ISiteContext siteContext,
        CancellationToken ct)
    {
        var existing = await pagesActor.GetByIdAsync(id, siteContext.SiteId, ct);
        if (!string.IsNullOrWhiteSpace(existing.error.Message))
            return TypedResults.NotFound(existing.error);

        var result = await pagesActor.DeleteMultipleAsync([id], siteContext.SiteId, true, ct);
        if (result.NotFound)
            return TypedResults.NotFound(new { error = $"Page with id '{id}' not found." });
        if (!string.IsNullOrWhiteSpace(result.Error))
            return TypedResults.Problem(result.Error);

        return result.Deleted == 0
            ? TypedResults.NotFound(new { error = $"Page with id '{id}' not found." })
            : TypedResults.Ok(true);
    }

    private static async Task<IResult> DeleteMultiplePages(
        [FromBody] DeleteMultiplePagesRequest request,
        [FromServices] IAeroPageActor pagesActor,
        [FromServices] ISiteContext siteContext,
        CancellationToken ct)
    {
        var result = await pagesActor.DeleteMultipleAsync(
            request.Ids.ToArray(),
            siteContext.SiteId,
            request.DeleteDescendants,
            ct);
        if (result.NotFound)
            return TypedResults.NotFound(new { error = "One or more pages were not found." });
        if (!string.IsNullOrWhiteSpace(result.Error))
            return TypedResults.Problem(result.Error);

        return TypedResults.Ok(new { deleted = result.Deleted });
    }

    private static async Task<IResult> PublishPage(
        long id,
        [FromServices] IAeroPageActor pagesActor,
        [FromServices] ILoggerFactory loggerFactory,
        [FromServices] ISiteContext siteContext,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(typeof(PagesApi));
        try
        {
            var result = await pagesActor.PublishAsync(id, siteContext.SiteId, ct);
            return !string.IsNullOrWhiteSpace(result.error.Message)
                ? TypedResults.NotFound(result.error)
                : TypedResults.Ok(MapToDetail(result.data));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error publishing page {Id}", id);
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> UnpublishPage(
        long id,
        [FromServices] IAeroPageActor pagesActor,
        [FromServices] ILoggerFactory loggerFactory,
        [FromServices] ISiteContext siteContext,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(typeof(PagesApi));
        try
        {
            var result = await pagesActor.UnpublishAsync(id, siteContext.SiteId, ct);
            return !string.IsNullOrWhiteSpace(result.error.Message)
                ? TypedResults.NotFound(result.error)
                : TypedResults.Ok(MapToDetail(result.data));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error unpublishing page {Id}", id);
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> SetPageTranslationGroupPublicationStateAsync(
        long translationGroupId,
        ContentPublicationState state,
        IPageContentService pageService,
        CancellationToken ct)
    {
        var variantsResult = await pageService.ListCultureVariantsAsync(translationGroupId, ct);
        if (variantsResult is Result<IReadOnlyList<PageDocument>, AeroError>.Failure variantsFailure)
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Failed to load page translations",
                Detail = variantsFailure.Error.ToString(),
                Status = StatusCodes.Status400BadRequest
            });
        }

        var variants = variantsResult is Result<IReadOnlyList<PageDocument>, AeroError>.Ok ok
            ? ok.Value
            : [];

        if (variants.Count == 0)
        {
            return TypedResults.NotFound(new ProblemDetails
            {
                Title = "No page translations found",
                Detail = $"No translated pages were found for translation group '{translationGroupId}'.",
                Status = StatusCodes.Status404NotFound
            });
        }

        var items = new List<PublicationBulkItem>();
        foreach (var page in variants)
        {
            page.PublicationState = state;
            page.PublishedOn = state == ContentPublicationState.Published
                ? page.PublishedOn ?? DateTimeOffset.UtcNow
                : null;

            var saveResult = await pageService.SaveAsync(page, ct);
            if (saveResult is Result<PageDocument, AeroError>.Failure saveFailure)
            {
                return TypedResults.BadRequest(new ProblemDetails
                {
                    Title = "Failed to update page publication state",
                    Detail = saveFailure.Error.ToString(),
                    Status = StatusCodes.Status400BadRequest
                });
            }

            if (saveResult is Result<PageDocument, AeroError>.Ok saveOk)
            {
                items.Add(new PublicationBulkItem(
                    saveOk.Value.Id,
                    saveOk.Value.Culture,
                    saveOk.Value.Title,
                    saveOk.Value.PublicationState == ContentPublicationState.Published));
            }
        }

        return TypedResults.Ok(new PublicationBulkResult(items.Count, items));
    }

    // ── Mapping helpers ────────────────────────────────────────────────

    private static PageDetail MapToDetail(PageViewModel vm)
    {
        return new PageDetail(
            vm.Id,
            vm.SiteId,
            vm.Title ?? "",
            vm.Slug ?? "",
            vm.Summary,
            vm.SeoTitle,
            vm.SeoDescription,
            vm.CreatedOn.DateTime,
            (vm.ModifiedOn ?? vm.CreatedOn).DateTime,
            vm.PublishedOn?.DateTime,
            vm.PublicationState,
            0,
            vm.ShowInNavMenu,
            vm.ShowHeaderNavigation,
            vm.HideFooter,
            vm.ShowChatAgent,
            vm.ParentId,
            vm.Path ?? "",
            vm.Depth,
            vm.Culture,
            vm.TranslationGroupId,
            DeserializeDraftContent(vm.DraftContentJson),
            DeserializeDraftContent(vm.PublishedContentJson)
        );
    }

    private static PageDetail MapToDetail(PageDocument document)
        => new(
            document.Id,
            document.SiteId,
            document.Title ?? "",
            document.Slug ?? "",
            document.Summary,
            document.SeoTitle,
            document.SeoDescription,
            document.CreatedOn.DateTime,
            (document.ModifiedOn ?? document.CreatedOn).DateTime,
            document.PublishedOn?.DateTime,
            document.PublicationState,
            0,
            document.ShowInNavMenu,
            document.ShowHeaderNavigation,
            document.HideFooter,
            document.ShowChatAgent,
            document.ParentId,
            document.Path ?? "",
            document.Depth,
            document.Culture,
            document.TranslationGroupId,
            document.DraftContent,
            document.PublishedContent);

    private static string? SerializeDraftContent(HtmlPageContent? content) => content is null
        ? null
        : System.Text.Json.JsonSerializer.Serialize(content, HtmlJsonContext.Default.HtmlPageContent);

    private static HtmlPageContent? DeserializeDraftContent(string? json) => string.IsNullOrWhiteSpace(json)
        ? null
        : System.Text.Json.JsonSerializer.Deserialize(json, HtmlJsonContext.Default.HtmlPageContent);

    private static IReadOnlySet<string> GetSupportedCultures(SitesModel? site)
    {
        var cultures = site?.SupportedCultures.Count > 0
            ? site.SupportedCultures
            : [site?.DefaultCulture ?? SitesModel.DefaultCultureName];

        return cultures
            .Select(ContentSlugDocument.NormalizeCulture)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<AiTranslatedPagePlan> TranslatePagePlanAsync(
        PageDocument source,
        AiTranslatePagePlan plan,
        string? providerId,
        IAiContentTranslationService translationService,
        CancellationToken ct)
    {
        var fields = BuildTranslatableFields(source);
        if (fields.Count == 0)
        {
            return AiTranslatedPagePlan.Failed(plan, "The source page does not contain translatable content.");
        }

        var response = await translationService.TranslateAsync(
            new TranslateDocumentRequest(fields, source.Culture, plan.Culture, providerId),
            ct);

        return response switch
        {
            Result<TranslateDocumentResponse>.Ok ok => AiTranslatedPagePlan.Success(plan, ok.Value),
            Result<TranslateDocumentResponse>.Failure failure => AiTranslatedPagePlan.Failed(plan, GetErrorMessage(failure.Error)),
            _ => AiTranslatedPagePlan.Failed(plan, "Unexpected AI translation result.")
        };
    }

    private static List<TranslateDocumentField> BuildTranslatableFields(PageDocument source)
    {
        var fields = new List<TranslateDocumentField>
        {
            new("title", ContentFieldHint.Title, source.Title),
            new("slug", ContentFieldHint.Slug, source.Slug)
        };

        AddOptionalField(fields, "summary", ContentFieldHint.Excerpt, source.Summary);
        AddOptionalField(fields, "seoTitle", ContentFieldHint.SeoTitle, source.SeoTitle);
        AddOptionalField(fields, "seoDescription", ContentFieldHint.SeoDescription, source.SeoDescription);

        return fields;
    }

    private static async Task<AiTranslatePageCultureResult> SaveTranslatedPageAsync(
        long sourcePageId,
        AiTranslatePagePlan plan,
        TranslateDocumentResponse response,
        IPageContentService pageService,
        CancellationToken ct)
    {
        PageDocument target;
        if (plan.ExistingVariant is null)
        {
            var forkResult = await pageService.ForkPageForCultureAsync(
                sourcePageId,
                plan.Culture,
                GetTranslatedSlug(response, plan.Slug),
                ct);

            if (forkResult is not Result<PageDocument, AeroError>.Ok forkOk)
            {
                return FailedPageTranslation(plan.Culture, forkResult is Result<PageDocument, AeroError>.Failure failure
                    ? GetErrorMessage(failure.Error)
                    : "Failed to create translated page.");
            }

            target = forkOk.Value;
        }
        else
        {
            target = plan.ExistingVariant;
            target.Slug = plan.Slug;
            target.PublicationState = ContentPublicationState.Draft;
            target.PublishedOn = null;
        }

        ApplyTranslatedFields(target, response);
        var saveResult = await pageService.SaveAsync(target, ct);

        return saveResult switch
        {
            Result<PageDocument, AeroError>.Ok ok => new AiTranslatePageCultureResult(
                plan.Culture,
                true,
                MapToDetail(ok.Value),
                response.Warnings,
                null),
            Result<PageDocument, AeroError>.Failure failure => FailedPageTranslation(plan.Culture, GetErrorMessage(failure.Error)),
            _ => FailedPageTranslation(plan.Culture, "Failed to save translated page.")
        };
    }

    private static void ApplyTranslatedFields(PageDocument target, TranslateDocumentResponse response)
    {
        target.Title = GetTranslated(response, "title", target.Title);
        target.Summary = GetTranslated(response, "summary", target.Summary);
        target.SeoTitle = GetTranslated(response, "seoTitle", target.SeoTitle);
        target.SeoDescription = GetTranslated(response, "seoDescription", target.SeoDescription);
    }

    private static string GetTranslatedSlug(TranslateDocumentResponse response, string fallback)
    {
        var translated = GetTranslated(response, "slug", fallback);
        return string.IsNullOrWhiteSpace(translated)
            ? fallback
            : ContentSlugDocument.Normalize(translated);
    }

    private static string GetTranslated(TranslateDocumentResponse response, string key, string? fallback)
        => response.TranslatedFields.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback ?? string.Empty;

    private static void AddOptionalField(List<TranslateDocumentField> fields, string key, ContentFieldHint hint, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            fields.Add(new TranslateDocumentField(key, hint, value));
        }
    }

    private static AiTranslatePageCultureResult FailedPageTranslation(string culture, string error)
        => new(culture, false, null, [], error);

    private static string BuildDefaultLocalizedSlug(string slug, string culture)
    {
        var suffix = culture.ToLowerInvariant();
        var normalized = ContentSlugDocument.Normalize(slug);
        return string.IsNullOrWhiteSpace(normalized)
            ? suffix
            : $"{normalized}-{suffix}";
    }

    private static string GetErrorMessage(AeroError error) => error switch
    {
        AeroError.Error e => e.msg,
        AeroError.NotFound e => e.msg,
        AeroError.Conflict e => e.msg,
        AeroError.Database e => e.msg,
        AeroError.Unauthorized e => e.msg,
        AeroError.Forbidden e => e.msg,
        AeroError.Timeout e => e.msg,
        AeroError.InvalidRequest e => e.msg,
        AeroError.BadRequest e => e.msg,
        AeroError.Exists e => e.msg,
        AeroError.NullReferro e => e.msg,
        AeroError.Cancelled e => e.msg,
        AeroError.NotAllowed e => e.msg,
        AeroError.Configuration e => e.msg,
        AeroError.Validation e => string.Join("; ", e.Errors),
        AeroError.HttpRequest e => e.msg ?? "HTTP request error",
        _ => error.ToString()
    };

    private static bool CultureEquals(string left, string right)
        => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    // ── Preview handlers (moved from Headless PreviewApi) ──────────────

    private static async Task<IResult> PreviewPage(
        long id,
        [FromServices] IPageContentService pageService,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(typeof(PagesApi));
        try
        {
            var result = await pageService.LoadAsync(id, ct);
            if (result is Result<PageDocument?, AeroError>.Ok { Value: not null } ok)
                return TypedResults.Ok(new PreviewResponse<PageDocument>(ok.Value, "page"));

            return TypedResults.NotFound(new { error = $"Page with id '{id}' not found." });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error previewing page {Id}", id);
            return TypedResults.Json(new { error = "An error occurred processing your request." }, statusCode: 500);
        }
    }

    private static async Task<IResult> PreviewPageFragment(
        [FromBody] PreviewPageFragmentRequest request,
        [FromServices] ISiteContext siteContext,
        [FromServices] ISiteStyleProfileResolver styleProfileResolver,
        [FromServices] IStyleCompiler styleCompiler,
        [FromServices] HtmlStaticRenderer renderer,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(typeof(PagesApi));
        try
        {
            var profileResult = await styleProfileResolver.ResolveAsync(siteContext.SiteId, ct);
            if (profileResult is Result<IStyleProfile, AeroError>.Failure profileFailure)
                return TypedResults.BadRequest(new { error = profileFailure.Error.ToString() });

            var styleProfile = ((Result<IStyleProfile, AeroError>.Ok)profileResult).Value;
            var styles = styleCompiler.Compile(request.Content, styleProfile);
            if (styles is Result<CompiledPageStyles>.Failure styleFailure)
                return TypedResults.BadRequest(new { error = styleFailure.Error.ToString() });

            var rendered = renderer.RenderPage(
                request.Content,
                ((Result<CompiledPageStyles>.Ok)styles).Value);
            if (rendered is Result<RenderedHtmlPage>.Failure renderFailure)
                return TypedResults.BadRequest(new { error = renderFailure.Error.ToString() });

            var page = ((Result<RenderedHtmlPage>.Ok)rendered).Value;
            var html = string.IsNullOrWhiteSpace(page.CssText)
                ? page.Markup
                : $"<style data-aero-page-styles>{page.CssText}</style>{page.Markup}";

            return TypedResults.Ok(new PreviewPageFragmentResponse(html));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error rendering preview page fragment");
            return TypedResults.Json(new { error = "An error occurred rendering the preview fragment." }, statusCode: 500);
        }
    }

    // ── Preview helpers ─────────────────────────────────────────────────

    private static string RenderPreviewHtml(IHtmlContent content)
    {
        using var writer = new StringWriter();
        content.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }

    private sealed record AiTranslatePagePlan(
        string Culture,
        string Slug,
        PageDocument? ExistingVariant);

    private sealed record AiTranslatedPagePlan(
        AiTranslatePagePlan Plan,
        bool Succeeded,
        TranslateDocumentResponse? Response,
        string? Error)
    {
        /// <summary>Gets the target culture from the translation plan.</summary>
public string Culture => Plan.Culture;

        /// <summary>Creates a successful translated-plan result.</summary>
        /// <param name="plan">The target translation plan.</param>
        /// <param name="response">The translated document response.</param>
        /// <returns>A successful result containing the response.</returns>
public static AiTranslatedPagePlan Success(AiTranslatePagePlan plan, TranslateDocumentResponse response)
            => new(plan, true, response, null);

        /// <summary>Creates a failed translated-plan result.</summary>
        /// <param name="plan">The target translation plan.</param>
        /// <param name="error">The failure description.</param>
        /// <returns>A failed result without a translated response.</returns>
public static AiTranslatedPagePlan Failed(AiTranslatePagePlan plan, string error)
            => new(plan, false, null, error);
    }
}
