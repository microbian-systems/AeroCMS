using System.Globalization;
using Aero.Cms.Abstractions.Ai;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Navigation.Domain;
using Aero.Cms.Modules.Navigation.Events;
using Aero.Cms.Modules.Navigation.Services;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Aero.Cms.Modules.Navigation.Areas.Api.v1;

/// <summary>
/// Maps administrative navigation-menu editing, publication, translation, defaulting, archive, and history endpoints.
/// </summary>
/// <remarks>
/// The <c>admin/navigations</c> route group requires an authenticated principal and each endpoint
/// declares its exact site permission.
/// </remarks>
public static class NavigationAdminApi
{
    /// <summary>
    /// Maps the versioned administrative navigation endpoint group.
    /// </summary>
    /// <param name="app">The endpoint route builder receiving the group.</param>
public static void MapNavigationAdminApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/{HttpConstants.ApiPrefix}admin/navigations")
            .WithTags("Admin - Navigations")
            .RequireAuthorization();

        group.MapGet("/", ListNavigations)
            .RequireAuthorization("site:read")
            .WithName("ListNavigationMenus");

        group.MapGet("/{id:long}", GetNavigationById)
            .RequireAuthorization("site:read")
            .WithName("GetNavigationMenuById");

        group.MapGet("/details/{id:long}", GetNavigationById)
            .RequireAuthorization("site:read")
            .WithName("GetNavigationMenuDetailsById");

        group.MapGet("/{id:long}/translations", ListNavigationTranslations)
            .RequireAuthorization("site:read")
            .WithName("ListNavigationMenuTranslations");

        group.MapPost("/", CreateNavigation)
            .RequireAuthorization("site:create")
            .WithName("CreateNavigationMenu");

        group.MapPost("/{id:long}/translations", ForkNavigationToCulture)
            .RequireAuthorization("site:create")
            .WithName("ForkNavigationMenuToCulture");

        group.MapPost("/{id:long}/ai-translate", TranslateNavigationWithAi)
            .RequireAuthorization("site:create", "site:update")
            .WithName("TranslateNavigationMenuWithAi");

        group.MapPut("/{id:long}/draft", SaveDraft)
            .RequireAuthorization("site:update")
            .WithName("SaveNavigationMenuDraft");

        group.MapPut("/{id:long}/publish", Publish)
            .RequireAuthorization("site:update")
            .WithName("PublishNavigationMenu");

        group.MapPut("/{id:long}/default", SetDefault)
            .RequireAuthorization("site:update")
            .WithName("SetDefaultNavigationMenu");

        group.MapDelete("/{id:long}", Archive)
            .RequireAuthorization("site:delete")
            .WithName("ArchiveNavigationMenu");

        group.MapGet("/{id:long}/events", GetEvents)
            .RequireAuthorization("site:read")
            .WithName("GetNavigationMenuEvents");
    }

    /// <summary>
    /// Lists current-site menus and enriches each item with detail-derived count and version.
    /// </summary>
    /// <remarks>
    /// A detail lookup failure does not fail the page; that menu is returned with zero item count
    /// and version. The service constrains the initial list to the current manager site.
    /// </remarks>
    private static async Task<IResult> ListNavigations(
        [FromServices] INavMenuService service,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await service.ListAsync(skip, take, search, cancellationToken);
        if (result is Result<(IReadOnlyList<NavMenuDocument> Items, long TotalCount), AeroError>.Ok ok)
        {
            var summaries = new List<NavigationSummary>(ok.Value.Items.Count);
            foreach (var menu in ok.Value.Items)
            {
                var detail = await service.GetDetailAsync(menu.Id, cancellationToken);
                var itemCount = detail is Result<NavigationDetail, AeroError>.Ok detailOk
                    ? detailOk.Value.Items.Count
                    : 0;
                var version = detail is Result<NavigationDetail, AeroError>.Ok versionOk
                    ? versionOk.Value.Version
                    : 0;

                summaries.Add(new NavigationSummary(
                    menu.Id,
                    menu.Name,
                    menu.Key,
                    itemCount,
                    menu.CreatedOn.DateTime,
                    version,
                    menu.State.ToString(),
                    menu.Culture,
                    menu.TranslationGroupId));
            }

            return TypedResults.Ok(summaries);
        }

        return ToProblem(result);
    }

    /// <summary>
    /// Returns current-site editor detail for one navigation menu.
    /// </summary>
    private static async Task<IResult> GetNavigationById(
        long id,
        [FromServices] INavMenuService service,
        CancellationToken cancellationToken = default)
    {
        var result = await service.GetDetailAsync(id, cancellationToken);
        return result is Result<NavigationDetail, AeroError>.Ok ok
            ? TypedResults.Ok(ok.Value)
            : ToProblem(result);
    }

    /// <summary>
    /// Returns non-archived culture variants for the selected current-site menu.
    /// </summary>
    private static async Task<IResult> ListNavigationTranslations(
        long id,
        [FromServices] INavMenuService service,
        CancellationToken cancellationToken = default)
    {
        var result = await service.ListCultureVariantsAsync(id, cancellationToken);
        return result is Result<IReadOnlyList<NavigationDetail>, AeroError>.Ok ok
            ? TypedResults.Ok(ok.Value)
            : ToProblem(result);
    }

    /// <summary>
    /// Validates a create request, starts its event stream, and returns the resulting editor detail.
    /// </summary>
    private static async Task<IResult> CreateNavigation(
        [FromBody] CreateNavigationRequest request,
        [FromServices] INavMenuService service,
        [FromServices] IValidator<CreateNavigationRequest> validator,
        CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Invalid navigation menu",
                Detail = string.Join("; ", validation.Errors.Select(x => x.ErrorMessage)),
                Status = StatusCodes.Status400BadRequest
            });
        }

        var result = await service.CreateAsync(request, userId: null, cancellationToken);
        if (result is Result<NavMenuDocument, AeroError>.Ok ok)
        {
            var detail = await service.GetDetailAsync(ok.Value.Id, cancellationToken);
            return detail is Result<NavigationDetail, AeroError>.Ok detailOk
                ? TypedResults.Created($"/{HttpConstants.ApiPrefix}admin/navigations/{ok.Value.Id}", detailOk.Value)
                : ToProblem(detail);
        }

        return ToProblem(result);
    }

    /// <summary>
    /// Creates a draft culture fork and returns its editor detail.
    /// </summary>
    private static async Task<IResult> ForkNavigationToCulture(
        long id,
        [FromBody] ForkNavigationCultureRequest request,
        [FromServices] INavMenuService service,
        CancellationToken cancellationToken = default)
    {
        var result = await service.ForkToCultureAsync(id, request.Culture, userId: null, cancellationToken);
        return result is Result<NavMenuDocument, AeroError>.Ok ok
            ? await ToNavigationDetailResult(ok.Value.Id, service, cancellationToken)
            : ToProblem(result);
    }

    /// <summary>
    /// Translates eligible navigation fields for distinct supported target cultures in parallel.
    /// </summary>
    /// <remarks>
    /// Per-culture translation or save failures are returned inside a successful aggregate response.
    /// Existing variants are skipped unless overwrite is requested; overwrite saves a new draft but
    /// does not publish it. Newly forked variants can remain persisted even if the later draft save fails.
    /// </remarks>
    private static async Task<IResult> TranslateNavigationWithAi(
        long id,
        [FromBody] AiTranslateNavigationRequest request,
        [FromServices] INavMenuService service,
        [FromServices] IQuerySession query,
        [FromServices] IAiContentTranslationService translationService,
        CancellationToken cancellationToken = default)
    {
        if (request.Targets.Count == 0)
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "No target cultures",
                Detail = "At least one target culture is required."
            });
        }

        var sourceDocumentResult = await service.GetAsync(id, cancellationToken);
        if (sourceDocumentResult is not Result<NavMenuDocument, AeroError>.Ok sourceDocumentOk)
        {
            return ToProblem(sourceDocumentResult);
        }

        var sourceDetailResult = await service.GetDetailAsync(id, cancellationToken);
        if (sourceDetailResult is not Result<NavigationDetail, AeroError>.Ok sourceDetailOk)
        {
            return ToProblem(sourceDetailResult);
        }

        var sourceDocument = sourceDocumentOk.Value;
        var source = sourceDetailOk.Value;
        var supportedCultures = await GetSupportedCulturesAsync(query, sourceDocument.SiteId, cancellationToken);
        var variantsResult = await service.ListCultureVariantsAsync(source.Id, cancellationToken);
        var variants = variantsResult is Result<IReadOnlyList<NavigationDetail>, AeroError>.Ok variantsOk
            ? variantsOk.Value
            : [source];

        var immediateResults = new List<AiTranslateNavigationCultureResult>();
        var plans = new List<AiTranslateNavigationPlan>();
        var plannedCultures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var target in request.Targets)
        {
            var culture = NormalizeCultureName(target.Culture);
            if (!plannedCultures.Add(culture))
            {
                continue;
            }

            if (CultureEquals(culture, source.Culture))
            {
                immediateResults.Add(FailedNavigationTranslation(culture, "Target culture must be different from the source culture."));
                continue;
            }

            if (!supportedCultures.Contains(culture))
            {
                immediateResults.Add(FailedNavigationTranslation(culture, $"Culture '{culture}' is not supported by this site."));
                continue;
            }

            var existing = variants.FirstOrDefault(x => CultureEquals(x.Culture, culture));
            if (existing is not null && !request.OverwriteExisting)
            {
                immediateResults.Add(FailedNavigationTranslation(culture, $"A '{culture}' translation already exists."));
                continue;
            }

            plans.Add(new AiTranslateNavigationPlan(culture, existing));
        }

        var translatedPlans = await Task.WhenAll(plans.Select(plan =>
            TranslateNavigationPlanAsync(source, plan, request.ProviderId, translationService, cancellationToken)));

        var results = new List<AiTranslateNavigationCultureResult>(immediateResults);
        foreach (var translated in translatedPlans)
        {
            if (!translated.Succeeded || translated.Response is null)
            {
                results.Add(FailedNavigationTranslation(translated.Culture, translated.Error ?? "AI translation failed."));
                continue;
            }

            results.Add(await SaveTranslatedNavigationAsync(
                source.Id,
                translated.Plan,
                translated.Response,
                service,
                cancellationToken));
        }

        return TypedResults.Ok(new AiTranslateNavigationResult(results
            .OrderBy(x => x.Culture, StringComparer.OrdinalIgnoreCase)
            .ToList()));
    }

    /// <summary>
    /// Validates and saves a draft using the preferred version or legacy revision query token.
    /// </summary>
    private static async Task<IResult> SaveDraft(
        long id,
        [FromBody] UpdateNavigationRequest request,
        [FromServices] INavMenuService service,
        [FromServices] IValidator<UpdateNavigationRequest> validator,
        [FromQuery] long? expectedVersion,
        [FromQuery] long? expectedRevision,
        CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Invalid navigation menu",
                Detail = string.Join("; ", validation.Errors.Select(x => x.ErrorMessage)),
                Status = StatusCodes.Status400BadRequest
            });
        }

        var result = await service.SaveDraftAsync(id, request, expectedVersion ?? expectedRevision ?? 0, userId: null, cancellationToken);
        return result is Result<NavMenuDocument, AeroError>.Ok ok
            ? await ToNavigationDetailResult(ok.Value.Id, service, cancellationToken)
            : ToProblem(result);
    }

    /// <summary>
    /// Publishes the latest draft using the preferred version or legacy revision query token.
    /// </summary>
    private static async Task<IResult> Publish(
        long id,
        [FromServices] INavMenuService service,
        [FromQuery] long expectedVersion,
        [FromQuery] long? expectedRevision,
        CancellationToken cancellationToken = default)
    {
        var result = await service.PublishAsync(id, expectedVersion == 0 ? expectedRevision ?? 0 : expectedVersion, userId: null, cancellationToken);
        return result is Result<NavMenuDocument, AeroError>.Ok ok
            ? await ToNavigationDetailResult(ok.Value.Id, service, cancellationToken)
            : ToProblem(result);
    }

    /// <summary>
    /// Sets a published current-site menu as the site default.
    /// </summary>
    private static async Task<IResult> SetDefault(
        long id,
        [FromServices] INavMenuService service,
        CancellationToken cancellationToken = default)
    {
        var result = await service.SetDefaultAsync(id, userId: null, cancellationToken);
        return result is Result<bool, AeroError>.Ok
            ? TypedResults.Ok(true)
            : ToProblem(result);
    }

    /// <summary>
    /// Archives a current-site menu using the preferred version or legacy revision token.
    /// </summary>
    private static async Task<IResult> Archive(
        long id,
        [FromServices] INavMenuService service,
        [FromQuery] long? expectedVersion,
        [FromQuery] long? expectedRevision,
        CancellationToken cancellationToken = default)
    {
        var result = await service.ArchiveAsync(id, expectedVersion ?? expectedRevision ?? 0, userId: null, cancellationToken);
        return result is Result<bool, AeroError>.Ok
            ? TypedResults.Ok(true)
            : ToProblem(result);
    }

    /// <summary>
    /// Converts a successful mutation's menu identifier into a fresh editor-detail response.
    /// </summary>
    /// <param name="id">The mutated menu identifier.</param>
    /// <param name="service">The navigation service used for the detail read.</param>
    /// <param name="cancellationToken">The request-abort token.</param>
    /// <returns>An OK detail response or a mapped problem result.</returns>
    private static async Task<IResult> ToNavigationDetailResult(
        long id,
        INavMenuService service,
        CancellationToken cancellationToken)
    {
        var detail = await service.GetDetailAsync(id, cancellationToken);
        return detail is Result<NavigationDetail, AeroError>.Ok ok
            ? TypedResults.Ok(ok.Value)
            : ToProblem(detail);
    }

    /// <summary>
    /// Returns event metadata for the identifier-derived navigation stream.
    /// </summary>
    /// <param name="id">The navigation stream identifier.</param>
    /// <param name="querySession">The event query session.</param>
    /// <param name="cancellationToken">The request-abort token.</param>
    /// <returns>The ordered stream history.</returns>
    private static async Task<IResult> GetEvents(
        long id,
        [FromServices] INavMenuService service,
        [FromServices] IQuerySession querySession,
        CancellationToken cancellationToken)
    {
        var existing = await service.GetAsync(id, cancellationToken);
        if (existing is Result<NavMenuDocument, AeroError>.Failure)
        {
            return ToProblem(existing);
        }

        var events = await querySession.Events.FetchStreamAsync(NavMenuStreams.Menu(id), ct: cancellationToken);
        var history = events.Select(e => new NavigationEventItem(
            e.Version,
            e.EventType.Name,
            e.Timestamp,
            e.StreamId.Value ?? NavMenuStreams.Menu(id),
            e.Data is NavMenuArchived)).ToList();

        return TypedResults.Ok(new NavigationEventHistory(id, history.Count, history));
    }

    /// <summary>
    /// Loads and normalizes the site's configured translation targets.
    /// </summary>
    /// <param name="query">The site query session.</param>
    /// <param name="siteId">The source menu's site identifier.</param>
    /// <param name="cancellationToken">The request-abort token.</param>
    /// <returns>A case-insensitive set, falling back to the site's default or platform default culture.</returns>
    private static async Task<IReadOnlySet<string>> GetSupportedCulturesAsync(
        IQuerySession query,
        long siteId,
        CancellationToken cancellationToken)
    {
        var site = await query.LoadAsync<SitesModel>(siteId, cancellationToken);
        var cultures = site?.SupportedCultures.Count > 0
            ? site.SupportedCultures
            : [site?.DefaultCulture ?? SitesModel.DefaultCultureName];

        return cultures
            .Select(NormalizeCultureName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Sends one target culture's nonblank fields to the configured AI translation service.
    /// </summary>
    /// <param name="source">The source navigation detail.</param>
    /// <param name="plan">The target culture and optional existing variant.</param>
    /// <param name="providerId">An optional AI provider profile identifier.</param>
    /// <param name="translationService">The translation abstraction.</param>
    /// <param name="cancellationToken">The request-abort token.</param>
    /// <returns>A success or failure plan without throwing domain failures.</returns>
    private static async Task<AiTranslatedNavigationPlan> TranslateNavigationPlanAsync(
        NavigationDetail source,
        AiTranslateNavigationPlan plan,
        string? providerId,
        IAiContentTranslationService translationService,
        CancellationToken cancellationToken)
    {
        var fields = BuildTranslatableFields(source);
        if (fields.Count == 0)
        {
            return AiTranslatedNavigationPlan.Failed(plan, "The source header menu does not contain translatable content.");
        }

        var response = await translationService.TranslateAsync(
            new TranslateDocumentRequest(fields, source.Culture, plan.Culture, providerId),
            cancellationToken);

        return response switch
        {
            Result<TranslateDocumentResponse>.Ok ok => AiTranslatedNavigationPlan.Success(plan, ok.Value),
            Result<TranslateDocumentResponse>.Failure failure => AiTranslatedNavigationPlan.Failed(plan, GetErrorMessage(failure.Error)),
            _ => AiTranslatedNavigationPlan.Failed(plan, "Unexpected AI translation result.")
        };
    }

    /// <summary>
    /// Creates stable translation keys for the menu name, title, and legacy item labels and alt text.
    /// </summary>
    /// <param name="source">The source navigation detail.</param>
    /// <returns>Only fields whose source values are nonblank.</returns>
    private static List<TranslateDocumentField> BuildTranslatableFields(NavigationDetail source)
    {
        var fields = new List<TranslateDocumentField>();
        AddOptionalField(fields, "name", ContentFieldHint.GroupName, source.Name);
        AddOptionalField(fields, "title", ContentFieldHint.BlockText, source.Title);

        for (var i = 0; i < source.Items.Count; i++)
        {
            AddOptionalField(fields, $"items.{i}.label", ContentFieldHint.Label, source.Items[i].Label);
            AddOptionalField(fields, $"items.{i}.altText", ContentFieldHint.AltText, source.Items[i].AltText);
        }

        return fields;
    }

    /// <summary>
    /// Forks a missing variant or reuses an existing one, then saves translated legacy fields as a draft.
    /// </summary>
    /// <param name="sourceId">The source menu used for a new culture fork.</param>
    /// <param name="plan">The target culture and optional existing variant.</param>
    /// <param name="response">The translated fields and warnings.</param>
    /// <param name="service">The navigation service used for fork, save, and detail reads.</param>
    /// <param name="cancellationToken">The request-abort token.</param>
    /// <returns>A per-culture result containing saved detail or an error message.</returns>
    /// <remarks>
    /// A newly forked stream is committed before the translated draft is saved, so later failure
    /// does not roll the fork back.
    /// </remarks>
    private static async Task<AiTranslateNavigationCultureResult> SaveTranslatedNavigationAsync(
        long sourceId,
        AiTranslateNavigationPlan plan,
        TranslateDocumentResponse response,
        INavMenuService service,
        CancellationToken cancellationToken)
    {
        NavigationDetail target;
        if (plan.ExistingVariant is null)
        {
            var forkResult = await service.ForkToCultureAsync(sourceId, plan.Culture, userId: null, cancellationToken);
            if (forkResult is not Result<NavMenuDocument, AeroError>.Ok forkOk)
            {
                return FailedNavigationTranslation(plan.Culture, forkResult is Result<NavMenuDocument, AeroError>.Failure forkFailure
                    ? GetErrorMessage(forkFailure.Error)
                    : "Failed to create translated header menu.");
            }

            var detailResult = await service.GetDetailAsync(forkOk.Value.Id, cancellationToken);
            if (detailResult is not Result<NavigationDetail, AeroError>.Ok detailOk)
            {
                return FailedNavigationTranslation(plan.Culture, detailResult is Result<NavigationDetail, AeroError>.Failure detailFailure
                    ? GetErrorMessage(detailFailure.Error)
                    : "Failed to load translated header menu.");
            }

            target = detailOk.Value;
        }
        else
        {
            target = plan.ExistingVariant;
        }

        var request = BuildTranslatedRequest(target, response);
        var saveResult = await service.SaveDraftAsync(target.Id, request, target.Version, userId: null, cancellationToken);
        if (saveResult is not Result<NavMenuDocument, AeroError>.Ok saveOk)
        {
            return FailedNavigationTranslation(plan.Culture, saveResult is Result<NavMenuDocument, AeroError>.Failure saveFailure
                ? GetErrorMessage(saveFailure.Error)
                : "Failed to save translated header menu.");
        }

        var savedDetail = await service.GetDetailAsync(saveOk.Value.Id, cancellationToken);
        return savedDetail is Result<NavigationDetail, AeroError>.Ok ok
            ? new AiTranslateNavigationCultureResult(plan.Culture, true, ok.Value, response.Warnings, null)
            : FailedNavigationTranslation(plan.Culture, savedDetail is Result<NavigationDetail, AeroError>.Failure savedFailure
                ? GetErrorMessage(savedFailure.Error)
                : "Failed to load saved header menu.");
    }

    /// <summary>
    /// Overlays translated legacy text fields while preserving link destinations and logo metadata.
    /// </summary>
    /// <param name="target">The target variant's current editor detail.</param>
    /// <param name="response">The translation response keyed by source field paths.</param>
    /// <returns>An update request containing translated name, title, labels, and alt text.</returns>
    /// <remarks>
    /// This compatibility mapping uses <see cref="NavigationDetail.Items"/> and therefore does not
    /// carry row/canvas or non-link component structures into the translated save request.
    /// </remarks>
    private static UpdateNavigationRequest BuildTranslatedRequest(NavigationDetail target, TranslateDocumentResponse response)
        => new(
            GetTranslated(response, "name", target.Name),
            GetTranslated(response, "title", target.Title),
            target.Items
                .OrderBy(x => x.Order)
                .Select((item, index) => new UpdateNavigationItemRequest(
                    item.Id,
                    GetTranslated(response, $"items.{index}.label", item.Label),
                    item.Url,
                    item.PageId,
                    item.Order,
                    GetTranslated(response, $"items.{index}.altText", item.AltText),
                    item.IsExternal,
                    item.Target))
                .ToList(),
            target.SiteLogoUrl);

    /// <summary>
    /// Canonicalizes recognized culture names for comparison.
    /// </summary>
    /// <param name="culture">The candidate culture.</param>
    /// <returns>The platform default for blank input, a canonical name when recognized, or trimmed invalid text.</returns>
    private static string NormalizeCultureName(string? culture)
    {
        if (string.IsNullOrWhiteSpace(culture))
        {
            return SitesModel.DefaultCultureName;
        }

        try
        {
            return CultureInfo.GetCultureInfo(culture.Trim()).Name;
        }
        catch (CultureNotFoundException)
        {
            return culture.Trim();
        }
    }

    /// <summary>
    /// Reads a nonblank translated field or falls back to the existing value.
    /// </summary>
    /// <param name="response">The translation response.</param>
    /// <param name="key">The stable field key.</param>
    /// <param name="fallback">The current value.</param>
    /// <returns>The translated value, fallback, or empty string.</returns>
    private static string GetTranslated(TranslateDocumentResponse response, string key, string? fallback)
        => response.TranslatedFields.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback ?? string.Empty;

    /// <summary>
    /// Adds a translation field only when its source text is nonblank.
    /// </summary>
    /// <param name="fields">The destination field list.</param>
    /// <param name="key">The stable response key.</param>
    /// <param name="hint">The semantic translation hint.</param>
    /// <param name="value">The source text.</param>
    private static void AddOptionalField(List<TranslateDocumentField> fields, string key, ContentFieldHint hint, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            fields.Add(new TranslateDocumentField(key, hint, value));
        }
    }

    /// <summary>
    /// Creates a consistent per-culture failed translation result.
    /// </summary>
    /// <param name="culture">The target culture.</param>
    /// <param name="error">The user-facing error.</param>
    /// <returns>A failed result with no detail or warnings.</returns>
    private static AiTranslateNavigationCultureResult FailedNavigationTranslation(string culture, string error)
        => new(culture, false, null, [], error);

    /// <summary>
    /// Compares canonical or user-supplied culture names case-insensitively.
    /// </summary>
    /// <param name="left">The first culture.</param>
    /// <param name="right">The second culture.</param>
    /// <returns>Whether the names are equal ignoring case.</returns>
    private static bool CultureEquals(string left, string right)
        => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Extracts a concise message from every supported railway error variant.
    /// </summary>
    /// <param name="error">The domain error.</param>
    /// <returns>The embedded message or a stable fallback.</returns>
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

    /// <summary>
    /// Maps selected railway failures to administrative HTTP responses.
    /// </summary>
    /// <typeparam name="T">The success payload type.</typeparam>
    /// <param name="result">The service result to map.</param>
    /// <returns>
    /// A 404, 409, or 400 response for recognized failures; otherwise a generic problem response.
    /// </returns>
    private static IResult ToProblem<T>(Result<T, AeroError> result)
        => result is Result<T, AeroError>.Failure failure
            ? failure.Error switch
            {
                AeroError.NotFound e => TypedResults.NotFound(new { error = e.msg }),
                AeroError.Conflict e => TypedResults.Conflict(new ProblemDetails { Title = "Conflict", Detail = e.msg, Status = StatusCodes.Status409Conflict }),
                AeroError.Validation e => TypedResults.BadRequest(new ProblemDetails { Title = "Validation failed", Detail = string.Join("; ", e.Errors), Status = StatusCodes.Status400BadRequest }),
                AeroError.InvalidRequest e => TypedResults.BadRequest(new ProblemDetails { Title = "Invalid request", Detail = e.msg, Status = StatusCodes.Status400BadRequest }),
                _ => TypedResults.Problem(failure.Error.ToString())
            }
            : TypedResults.Problem("Unexpected navigation API result.");
}

/// <summary>
/// Describes one event in an administrative navigation history response.
/// </summary>
/// <param name="Version">The event's stream version.</param>
/// <param name="EventType">The persisted event type name.</param>
/// <param name="Timestamp">The event-store timestamp.</param>
/// <param name="StreamKey">The stream key containing the event.</param>
/// <param name="IsArchived">Whether the payload is a <see cref="NavMenuArchived"/> event.</param>
public sealed record NavigationEventItem(
    long Version,
    string EventType,
    DateTimeOffset Timestamp,
    string StreamKey,
    bool IsArchived);

/// <summary>
/// Contains the complete event metadata returned for a navigation stream.
/// </summary>
/// <param name="NavMenuId">The requested navigation menu identifier.</param>
/// <param name="TotalEvents">The number of returned events.</param>
/// <param name="Events">The event metadata in event-store order.</param>
public sealed record NavigationEventHistory(
    long NavMenuId,
    int TotalEvents,
    IReadOnlyList<NavigationEventItem> Events);

/// <summary>
/// Describes one eligible AI translation target and its optional existing culture variant.
/// </summary>
/// <param name="Culture">The normalized target culture.</param>
/// <param name="ExistingVariant">The current variant to overwrite, or <see langword="null"/> to fork.</param>
internal sealed record AiTranslateNavigationPlan(
    string Culture,
    NavigationDetail? ExistingVariant);

/// <summary>
/// Captures the translation-service outcome before any translated variant is saved.
/// </summary>
/// <param name="Plan">The target plan.</param>
/// <param name="Succeeded">Whether translation produced a response.</param>
/// <param name="Response">The translated fields and warnings on success.</param>
/// <param name="Error">The failure message when translation did not succeed.</param>
internal sealed record AiTranslatedNavigationPlan(
    AiTranslateNavigationPlan Plan,
    bool Succeeded,
    TranslateDocumentResponse? Response,
    string? Error)
{
    /// <summary>
    /// Gets the target culture from <see cref="Plan"/>.
    /// </summary>
public string Culture => Plan.Culture;

    /// <summary>
    /// Creates a successful translated plan.
    /// </summary>
    /// <param name="plan">The completed target plan.</param>
    /// <param name="response">The translation-service response.</param>
    /// <returns>A successful outcome with no error.</returns>
public static AiTranslatedNavigationPlan Success(AiTranslateNavigationPlan plan, TranslateDocumentResponse response)
        => new(plan, true, response, null);

    /// <summary>
    /// Creates a failed translated plan.
    /// </summary>
    /// <param name="plan">The failed target plan.</param>
    /// <param name="error">The failure message.</param>
    /// <returns>A failed outcome with no translation response.</returns>
public static AiTranslatedNavigationPlan Failed(AiTranslateNavigationPlan plan, string error)
        => new(plan, false, null, error);
}
