using System.Globalization;
using Aero.Cms.Abstractions.Ai;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Footer.Domain;
using Aero.Cms.Modules.Footer.Events;
using Aero.Cms.Modules.Footer.Services;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Aero.Cms.Modules.Footer.Areas.Api.v1;

/// <summary>
/// Maps the minimal API surface for footer administration, culture variants, publication, and history.
/// </summary>
/// <remarks>
/// Most authoring operations enforce current-site ownership through <see cref="IFooterService"/>.
/// Event-history lookup reads the requested stream directly and does not perform that ownership check.
/// The route group does not attach authorization or rate-limiting metadata, so the host must secure it.
/// </remarks>
public static class FooterAdminApi
{
    /// <summary>
    /// Maps footer administration endpoints beneath the configured API prefix and <c>admin/footers</c>.
    /// </summary>
    /// <param name="app">The endpoint route builder that receives the footer route group.</param>
    /// <remarks>
    /// The mapped surface includes list/detail, culture fork, multi-culture AI translation, draft save,
    /// publish, default selection, archive, and raw event history. AI translation translates selected
    /// textual fields only, saves drafts without publishing, and can partially succeed across target
    /// cultures because the per-culture changes are not transactional as a group. Cancellation tokens
    /// are forwarded by the handlers. Service failures are converted to HTTP problem responses.
    /// </remarks>
    public static void MapFooterAdminApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/{HttpConstants.ApiPrefix}admin/footers")
            .WithTags("Admin - Footers");

        group.MapGet("/", ListFooters).WithName("ListFooters");
        group.MapGet("/{id:long}", GetFooterById).WithName("GetFooterById");
        group.MapGet("/details/{id:long}", GetFooterById).WithName("GetFooterDetailsById");
        group.MapGet("/{id:long}/translations", ListFooterTranslations).WithName("ListFooterTranslations");
        group.MapPost("/", CreateFooter).WithName("CreateFooter");
        group.MapPost("/{id:long}/translations", ForkFooterToCulture).WithName("ForkFooterToCulture");
        group.MapPost("/{id:long}/ai-translate", TranslateFooterWithAi).WithName("TranslateFooterWithAi");
        group.MapPut("/{id:long}", SaveDraftCompatibility).WithName("UpdateFooter");
        group.MapPut("/{id:long}/draft", SaveDraft).WithName("SaveFooterDraft");
        group.MapPut("/{id:long}/publish", Publish).WithName("PublishFooter");
        group.MapPut("/{id:long}/default", SetDefault).WithName("SetDefaultFooter");
        group.MapDelete("/{id:long}", Archive).WithName("ArchiveFooter");
        group.MapGet("/{id:long}/events", GetEvents).WithName("GetFooterEvents");
    }

    private static async Task<IResult> ListFooters(
        [FromServices] IFooterService service,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await service.ListAsync(skip, take, search, cancellationToken);
        if (result is Result<(IReadOnlyList<FooterDocument> Items, long TotalCount), AeroError>.Ok ok)
        {
            var summaries = new List<FooterSummary>(ok.Value.Items.Count);
            foreach (var footer in ok.Value.Items)
            {
                var detail = await service.GetDetailAsync(footer.Id, cancellationToken);
                var itemCount = detail is Result<FooterDetail, AeroError>.Ok detailOk ? detailOk.Value.LinkGroups.Count : 0;
                var version = detail is Result<FooterDetail, AeroError>.Ok versionOk ? versionOk.Value.Version : 0;

                summaries.Add(new FooterSummary(
                    footer.Id,
                    footer.Name,
                    footer.Description,
                    itemCount,
                    footer.CreatedOn.DateTime,
                    version,
                    footer.State.ToString(),
                    footer.Culture,
                    footer.TranslationGroupId));
            }

            return TypedResults.Ok(summaries);
        }

        return ToProblem(result);
    }

    private static async Task<IResult> GetFooterById(
        long id,
        [FromServices] IFooterService service,
        CancellationToken cancellationToken = default)
    {
        var result = await service.GetDetailAsync(id, cancellationToken);
        return result is Result<FooterDetail, AeroError>.Ok ok
            ? TypedResults.Ok(ok.Value)
            : ToProblem(result);
    }

    private static async Task<IResult> ListFooterTranslations(
        long id,
        [FromServices] IFooterService service,
        CancellationToken cancellationToken = default)
    {
        var result = await service.ListCultureVariantsAsync(id, cancellationToken);
        return result is Result<IReadOnlyList<FooterDetail>, AeroError>.Ok ok
            ? TypedResults.Ok(ok.Value)
            : ToProblem(result);
    }

    private static async Task<IResult> CreateFooter(
        [FromBody] CreateFooterRequest request,
        [FromServices] IFooterService service,
        [FromServices] IValidator<CreateFooterRequest> validator,
        CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem("Invalid footer", validation.Errors.Select(x => x.ErrorMessage));
        }

        var result = await service.CreateAsync(request, userId: null, cancellationToken);
        if (result is Result<FooterDocument, AeroError>.Ok ok)
        {
            var detail = await service.GetDetailAsync(ok.Value.Id, cancellationToken);
            return detail is Result<FooterDetail, AeroError>.Ok detailOk
                ? TypedResults.Created($"/{HttpConstants.ApiPrefix}admin/footers/{ok.Value.Id}", detailOk.Value)
                : ToProblem(detail);
        }

        return ToProblem(result);
    }

    private static async Task<IResult> ForkFooterToCulture(
        long id,
        [FromBody] ForkFooterCultureRequest request,
        [FromServices] IFooterService service,
        CancellationToken cancellationToken = default)
    {
        var result = await service.ForkToCultureAsync(id, request.Culture, userId: null, cancellationToken);
        return result is Result<FooterDocument, AeroError>.Ok ok
            ? await ToFooterDetailResult(ok.Value.Id, service, cancellationToken)
            : ToProblem(result);
    }

    private static async Task<IResult> TranslateFooterWithAi(
        long id,
        [FromBody] AiTranslateFooterRequest request,
        [FromServices] IFooterService service,
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
        if (sourceDocumentResult is not Result<FooterDocument, AeroError>.Ok sourceDocumentOk)
        {
            return ToProblem(sourceDocumentResult);
        }

        var sourceDetailResult = await service.GetDetailAsync(id, cancellationToken);
        if (sourceDetailResult is not Result<FooterDetail, AeroError>.Ok sourceDetailOk)
        {
            return ToProblem(sourceDetailResult);
        }

        var sourceDocument = sourceDocumentOk.Value;
        var source = sourceDetailOk.Value;
        var supportedCultures = await GetSupportedCulturesAsync(query, sourceDocument.SiteId, cancellationToken);
        var variantsResult = await service.ListCultureVariantsAsync(source.Id, cancellationToken);
        var variants = variantsResult is Result<IReadOnlyList<FooterDetail>, AeroError>.Ok variantsOk
            ? variantsOk.Value
            : [source];

        var immediateResults = new List<AiTranslateFooterCultureResult>();
        var plans = new List<AiTranslateFooterPlan>();
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
                immediateResults.Add(FailedFooterTranslation(culture, "Target culture must be different from the source culture."));
                continue;
            }

            if (!supportedCultures.Contains(culture))
            {
                immediateResults.Add(FailedFooterTranslation(culture, $"Culture '{culture}' is not supported by this site."));
                continue;
            }

            var existing = variants.FirstOrDefault(x => CultureEquals(x.Culture, culture));
            if (existing is not null && !request.OverwriteExisting)
            {
                immediateResults.Add(FailedFooterTranslation(culture, $"A '{culture}' translation already exists."));
                continue;
            }

            plans.Add(new AiTranslateFooterPlan(culture, existing));
        }

        var translatedPlans = await Task.WhenAll(plans.Select(plan =>
            TranslateFooterPlanAsync(source, plan, request.ProviderId, translationService, cancellationToken)));

        var results = new List<AiTranslateFooterCultureResult>(immediateResults);
        foreach (var translated in translatedPlans)
        {
            if (!translated.Succeeded || translated.Response is null)
            {
                results.Add(FailedFooterTranslation(translated.Culture, translated.Error ?? "AI translation failed."));
                continue;
            }

            results.Add(await SaveTranslatedFooterAsync(
                source.Id,
                translated.Plan,
                translated.Response,
                service,
                cancellationToken));
        }

        return TypedResults.Ok(new AiTranslateFooterResult(results
            .OrderBy(x => x.Culture, StringComparer.OrdinalIgnoreCase)
            .ToList()));
    }

    private static async Task<IResult> SaveDraftCompatibility(
        long id,
        [FromBody] UpdateFooterRequest request,
        [FromServices] IFooterService service,
        [FromServices] IValidator<UpdateFooterRequest> validator,
        CancellationToken cancellationToken = default)
    {
        var current = await service.GetAsync(id, cancellationToken);
        if (current is Result<FooterDocument, AeroError>.Failure)
        {
            return ToProblem(current);
        }

        return await SaveDraft(id, request, service, validator, expectedVersion: null, expectedRevision: null, cancellationToken);
    }

    private static async Task<IResult> SaveDraft(
        long id,
        [FromBody] UpdateFooterRequest request,
        [FromServices] IFooterService service,
        [FromServices] IValidator<UpdateFooterRequest> validator,
        [FromQuery] long? expectedVersion,
        [FromQuery] long? expectedRevision,
        CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem("Invalid footer", validation.Errors.Select(x => x.ErrorMessage));
        }

        var result = await service.SaveDraftAsync(id, request, expectedVersion ?? expectedRevision ?? 0, userId: null, cancellationToken);
        return result is Result<FooterDocument, AeroError>.Ok ok
            ? await ToFooterDetailResult(ok.Value.Id, service, cancellationToken)
            : ToProblem(result);
    }

    private static async Task<IResult> Publish(
        long id,
        [FromServices] IFooterService service,
        [FromQuery] long expectedVersion,
        [FromQuery] long? expectedRevision,
        CancellationToken cancellationToken = default)
    {
        var result = await service.PublishAsync(id, expectedVersion == 0 ? expectedRevision ?? 0 : expectedVersion, userId: null, cancellationToken);
        return result is Result<FooterDocument, AeroError>.Ok ok
            ? await ToFooterDetailResult(ok.Value.Id, service, cancellationToken)
            : ToProblem(result);
    }

    private static async Task<IResult> SetDefault(
        long id,
        [FromServices] IFooterService service,
        CancellationToken cancellationToken = default)
    {
        var result = await service.SetDefaultAsync(id, userId: null, cancellationToken);
        return result is Result<bool, AeroError>.Ok ? TypedResults.Ok(true) : ToProblem(result);
    }

    private static async Task<IResult> Archive(
        long id,
        [FromServices] IFooterService service,
        [FromQuery] long? expectedVersion,
        [FromQuery] long? expectedRevision,
        CancellationToken cancellationToken = default)
    {
        var result = await service.ArchiveAsync(id, expectedVersion ?? expectedRevision ?? 0, userId: null, cancellationToken);
        return result is Result<bool, AeroError>.Ok ? TypedResults.Ok(true) : ToProblem(result);
    }

    private static async Task<IResult> ToFooterDetailResult(long id, IFooterService service, CancellationToken cancellationToken)
    {
        var detail = await service.GetDetailAsync(id, cancellationToken);
        return detail is Result<FooterDetail, AeroError>.Ok ok ? TypedResults.Ok(ok.Value) : ToProblem(detail);
    }

    private static async Task<IResult> GetEvents(long id, IQuerySession querySession, CancellationToken cancellationToken)
    {
        var events = await querySession.Events.FetchStreamAsync(FooterStreams.Footer(id), ct: cancellationToken);
        var history = events.Select(e => new FooterEventItem(
            e.Version,
            e.EventType.Name,
            e.Timestamp,
            e.StreamId.Value ?? FooterStreams.Footer(id),
            e.Data is FooterArchived)).ToList();

        return TypedResults.Ok(new FooterEventHistory(id, history.Count, history));
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

    private static async Task<AiTranslatedFooterPlan> TranslateFooterPlanAsync(
        FooterDetail source,
        AiTranslateFooterPlan plan,
        string? providerId,
        IAiContentTranslationService translationService,
        CancellationToken cancellationToken)
    {
        var fields = BuildTranslatableFields(source);
        if (fields.Count == 0)
        {
            return AiTranslatedFooterPlan.Failed(plan, "The source footer does not contain translatable content.");
        }

        var response = await translationService.TranslateAsync(
            new TranslateDocumentRequest(fields, source.Culture, plan.Culture, providerId),
            cancellationToken);

        return response switch
        {
            Result<TranslateDocumentResponse>.Ok ok => AiTranslatedFooterPlan.Success(plan, ok.Value),
            Result<TranslateDocumentResponse>.Failure failure => AiTranslatedFooterPlan.Failed(plan, GetErrorMessage(failure.Error)),
            _ => AiTranslatedFooterPlan.Failed(plan, "Unexpected AI translation result.")
        };
    }

    private static List<TranslateDocumentField> BuildTranslatableFields(FooterDetail source)
    {
        var fields = new List<TranslateDocumentField>();
        AddOptionalField(fields, "name", ContentFieldHint.GroupName, source.Name);
        AddOptionalField(fields, "description", ContentFieldHint.BlockText, source.Description);
        AddOptionalField(fields, "companyName", ContentFieldHint.CompanyName, source.CompanyName);
        AddOptionalField(fields, "tagline", ContentFieldHint.Tagline, source.Tagline);
        AddOptionalField(fields, "copyrightText", ContentFieldHint.CopyrightText, source.CopyrightText);

        for (var i = 0; i < source.LinkGroups.Count; i++)
        {
            var group = source.LinkGroups[i];
            AddOptionalField(fields, $"groups.{i}.title", ContentFieldHint.GroupName, group.Title);
            for (var j = 0; j < group.Links.Count; j++)
            {
                AddOptionalField(fields, $"groups.{i}.links.{j}.label", ContentFieldHint.Label, group.Links[j].Label);
            }
        }

        for (var i = 0; i < source.LegalLinks.Count; i++)
        {
            AddOptionalField(fields, $"legalLinks.{i}.label", ContentFieldHint.Label, source.LegalLinks[i].Label);
        }

        return fields;
    }

    private static async Task<AiTranslateFooterCultureResult> SaveTranslatedFooterAsync(
        long sourceId,
        AiTranslateFooterPlan plan,
        TranslateDocumentResponse response,
        IFooterService service,
        CancellationToken cancellationToken)
    {
        FooterDetail target;
        if (plan.ExistingVariant is null)
        {
            var forkResult = await service.ForkToCultureAsync(sourceId, plan.Culture, userId: null, cancellationToken);
            if (forkResult is not Result<FooterDocument, AeroError>.Ok forkOk)
            {
                return FailedFooterTranslation(plan.Culture, forkResult is Result<FooterDocument, AeroError>.Failure forkFailure
                    ? GetErrorMessage(forkFailure.Error)
                    : "Failed to create translated footer.");
            }

            var detailResult = await service.GetDetailAsync(forkOk.Value.Id, cancellationToken);
            if (detailResult is not Result<FooterDetail, AeroError>.Ok detailOk)
            {
                return FailedFooterTranslation(plan.Culture, detailResult is Result<FooterDetail, AeroError>.Failure detailFailure
                    ? GetErrorMessage(detailFailure.Error)
                    : "Failed to load translated footer.");
            }

            target = detailOk.Value;
        }
        else
        {
            target = plan.ExistingVariant;
        }

        var request = BuildTranslatedRequest(target, response);
        var saveResult = await service.SaveDraftAsync(target.Id, request, target.Version, userId: null, cancellationToken);
        if (saveResult is not Result<FooterDocument, AeroError>.Ok saveOk)
        {
            return FailedFooterTranslation(plan.Culture, saveResult is Result<FooterDocument, AeroError>.Failure saveFailure
                ? GetErrorMessage(saveFailure.Error)
                : "Failed to save translated footer.");
        }

        var savedDetail = await service.GetDetailAsync(saveOk.Value.Id, cancellationToken);
        return savedDetail is Result<FooterDetail, AeroError>.Ok ok
            ? new AiTranslateFooterCultureResult(plan.Culture, true, ok.Value, response.Warnings, null)
            : FailedFooterTranslation(plan.Culture, savedDetail is Result<FooterDetail, AeroError>.Failure savedFailure
                ? GetErrorMessage(savedFailure.Error)
                : "Failed to load saved footer.");
    }

    private static UpdateFooterRequest BuildTranslatedRequest(FooterDetail target, TranslateDocumentResponse response)
        => new(
            GetTranslated(response, "name", target.Name),
            GetTranslated(response, "description", target.Description),
            GetTranslated(response, "companyName", target.CompanyName),
            target.LinkGroups
                .OrderBy(x => x.Order)
                .Select((group, groupIndex) => new UpdateFooterLinkGroupRequest(
                    group.Id,
                    GetTranslated(response, $"groups.{groupIndex}.title", group.Title),
                    group.Links
                        .OrderBy(x => x.Order)
                        .Select((link, linkIndex) => new UpdateFooterLinkRequest(
                            link.Id,
                            GetTranslated(response, $"groups.{groupIndex}.links.{linkIndex}.label", link.Label),
                            link.Href,
                            link.Order,
                            link.OpenInNewTab))
                        .ToList(),
                    group.Order))
                .ToList(),
            GetTranslated(response, "tagline", target.Tagline),
            target.LogoUrl,
            target.BackgroundImageUrl,
            target.OverlayOpacity,
            GetTranslated(response, "copyrightText", target.CopyrightText),
            target.LegalLinks
                .OrderBy(x => x.Order)
                .Select((link, index) => new UpdateFooterLinkRequest(
                    link.Id,
                    GetTranslated(response, $"legalLinks.{index}.label", link.Label),
                    link.Href,
                    link.Order,
                    link.OpenInNewTab))
                .ToList());

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

    private static AiTranslateFooterCultureResult FailedFooterTranslation(string culture, string error)
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

    private static IResult ValidationProblem(string title, IEnumerable<string> errors)
        => TypedResults.BadRequest(new ProblemDetails
        {
            Title = title,
            Detail = string.Join("; ", errors),
            Status = StatusCodes.Status400BadRequest
        });

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
            : TypedResults.Problem("Unexpected footer API result.");
}

/// <summary>
/// Describes one event returned by the footer event-history endpoint.
/// </summary>
/// <param name="Version">The event's version in the stream.</param>
/// <param name="EventType">The runtime event type name.</param>
/// <param name="Timestamp">The event timestamp reported by the event store.</param>
/// <param name="StreamKey">The footer event-stream key.</param>
/// <param name="IsArchived">Whether the event is a <see cref="FooterArchived"/> event.</param>
public sealed record FooterEventItem(long Version, string EventType, DateTimeOffset Timestamp, string StreamKey, bool IsArchived);

/// <summary>
/// Contains the complete event-history response for a footer stream.
/// </summary>
/// <param name="FooterId">The requested footer identifier.</param>
/// <param name="TotalEvents">The number of events returned.</param>
/// <param name="Events">The stream events in the order returned by the store.</param>
public sealed record FooterEventHistory(long FooterId, int TotalEvents, IReadOnlyList<FooterEventItem> Events);

internal sealed record AiTranslateFooterPlan(
    string Culture,
    FooterDetail? ExistingVariant);

internal sealed record AiTranslatedFooterPlan(
    AiTranslateFooterPlan Plan,
    bool Succeeded,
    TranslateDocumentResponse? Response,
    string? Error)
{
    /// <summary>Gets the target culture from the translation plan.</summary>
    public string Culture => Plan.Culture;

    /// <summary>Creates a successful per-culture translation result.</summary>
    /// <param name="plan">The source plan and any existing culture variant.</param>
    /// <param name="response">The translated text response.</param>
    /// <returns>A successful plan result.</returns>
    public static AiTranslatedFooterPlan Success(AiTranslateFooterPlan plan, TranslateDocumentResponse response)
        => new(plan, true, response, null);

    /// <summary>Creates a failed per-culture translation result without a translated response.</summary>
    /// <param name="plan">The source plan and any existing culture variant.</param>
    /// <param name="error">The failure description returned to the aggregate handler.</param>
    /// <returns>A failed plan result.</returns>
    public static AiTranslatedFooterPlan Failed(AiTranslateFooterPlan plan, string error)
        => new(plan, false, null, error);
}
