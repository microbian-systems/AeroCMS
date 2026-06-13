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

public static class NavigationAdminApi
{
    public static void MapNavigationAdminApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/{HttpConstants.ApiPrefix}admin/navigations")
            .WithTags("Admin - Navigations");

        group.MapGet("/", ListNavigations)
            .WithName("ListNavigationMenus");

        group.MapGet("/{id:long}", GetNavigationById)
            .WithName("GetNavigationMenuById");

        group.MapGet("/details/{id:long}", GetNavigationById)
            .WithName("GetNavigationMenuDetailsById");

        group.MapGet("/{id:long}/translations", ListNavigationTranslations)
            .WithName("ListNavigationMenuTranslations");

        group.MapPost("/", CreateNavigation)
            .WithName("CreateNavigationMenu");

        group.MapPost("/{id:long}/translations", ForkNavigationToCulture)
            .WithName("ForkNavigationMenuToCulture");

        group.MapPost("/{id:long}/ai-translate", TranslateNavigationWithAi)
            .WithName("TranslateNavigationMenuWithAi");

        group.MapPut("/{id:long}", SaveDraftCompatibility)
            .WithName("UpdateNavigationMenu");

        group.MapPut("/{id:long}/draft", SaveDraft)
            .WithName("SaveNavigationMenuDraft");

        group.MapPut("/{id:long}/publish", Publish)
            .WithName("PublishNavigationMenu");

        group.MapPut("/{id:long}/default", SetDefault)
            .WithName("SetDefaultNavigationMenu");

        group.MapDelete("/{id:long}", Archive)
            .WithName("ArchiveNavigationMenu");

        group.MapGet("/{id:long}/events", GetEvents)
            .WithName("GetNavigationMenuEvents");
    }

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

    private static async Task<IResult> SaveDraftCompatibility(
        long id,
        [FromBody] UpdateNavigationRequest request,
        [FromServices] INavMenuService service,
        [FromServices] IValidator<UpdateNavigationRequest> validator,
        CancellationToken cancellationToken = default)
    {
        var current = await service.GetAsync(id, cancellationToken);
        if (current is Result<NavMenuDocument, AeroError>.Failure)
        {
            return ToProblem(current);
        }

        return await SaveDraft(id, request, service, validator, expectedVersion: null, expectedRevision: null, cancellationToken);
    }

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

    private static async Task<IResult> GetEvents(
        long id,
        IQuerySession querySession,
        CancellationToken cancellationToken)
    {
        var events = await querySession.Events.FetchStreamAsync(NavMenuStreams.Menu(id), token: cancellationToken);
        var history = events.Select(e => new NavigationEventItem(
            e.Version,
            e.EventType.Name,
            e.Timestamp,
            e.StreamKey ?? NavMenuStreams.Menu(id),
            e.IsArchived)).ToList();

        return TypedResults.Ok(new NavigationEventHistory(id, history.Count, history));
    }

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

    private static AiTranslateNavigationCultureResult FailedNavigationTranslation(string culture, string error)
        => new(culture, false, null, [], error);

    private static bool CultureEquals(string left, string right)
        => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

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

public sealed record NavigationEventItem(
    long Version,
    string EventType,
    DateTimeOffset Timestamp,
    string StreamKey,
    bool IsArchived);

public sealed record NavigationEventHistory(
    long NavMenuId,
    int TotalEvents,
    IReadOnlyList<NavigationEventItem> Events);

internal sealed record AiTranslateNavigationPlan(
    string Culture,
    NavigationDetail? ExistingVariant);

internal sealed record AiTranslatedNavigationPlan(
    AiTranslateNavigationPlan Plan,
    bool Succeeded,
    TranslateDocumentResponse? Response,
    string? Error)
{
    public string Culture => Plan.Culture;

    public static AiTranslatedNavigationPlan Success(AiTranslateNavigationPlan plan, TranslateDocumentResponse response)
        => new(plan, true, response, null);

    public static AiTranslatedNavigationPlan Failed(AiTranslateNavigationPlan plan, string error)
        => new(plan, false, null, error);
}
