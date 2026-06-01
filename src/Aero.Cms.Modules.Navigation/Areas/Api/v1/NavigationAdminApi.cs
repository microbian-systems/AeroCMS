using Aero.Cms.Abstractions.Http.Clients;
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
