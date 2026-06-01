using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Modules.Footer.Domain;
using Aero.Cms.Modules.Footer.Events;
using Aero.Cms.Modules.Footer.Services;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Aero.Cms.Modules.Footer.Areas.Api.v1;

public static class FooterAdminApi
{
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
        var events = await querySession.Events.FetchStreamAsync(FooterStreams.Footer(id), token: cancellationToken);
        var history = events.Select(e => new FooterEventItem(
            e.Version,
            e.EventType.Name,
            e.Timestamp,
            e.StreamKey ?? FooterStreams.Footer(id),
            e.IsArchived)).ToList();

        return TypedResults.Ok(new FooterEventHistory(id, history.Count, history));
    }

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

public sealed record FooterEventItem(long Version, string EventType, DateTimeOffset Timestamp, string StreamKey, bool IsArchived);

public sealed record FooterEventHistory(long FooterId, int TotalEvents, IReadOnlyList<FooterEventItem> Events);
