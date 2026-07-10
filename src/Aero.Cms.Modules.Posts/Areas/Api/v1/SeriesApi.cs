using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Core.Entities;
using Aero.Core.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using ActorCreateSeriesRequest = Aero.Cms.Abstractions.Requests.CreateSeriesRequest;
using ActorDeleteSeriesRequest = Aero.Cms.Abstractions.Requests.DeleteSeriesRequest;
using ActorUpdateSeriesRequest = Aero.Cms.Abstractions.Requests.UpdateSeriesRequest;
using HttpCreateSeriesRequest = Aero.Cms.Abstractions.Http.Clients.CreateSeriesRequest;
using HttpUpdateSeriesRequest = Aero.Cms.Abstractions.Http.Clients.UpdateSeriesRequest;

namespace Aero.Cms.Modules.Posts.Areas.Api.v1;

/// <summary>
/// Thin admin API for post series management.
/// </summary>
public static class SeriesApi
{
    public static void MapSeriesApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/{HttpConstants.ApiPrefix}admin/series")
            .WithTags("Admin - Series");

        group.MapGet("/", GetAllSeries)
            .WithName("GetAllSeries");

        group.MapGet("/details/{id:long}", GetSeriesById)
            .WithName("GetSeriesById");

        group.MapGet("/{id:long}/translations", ListSeriesTranslations)
            .WithName("ListSeriesTranslations");

        group.MapPost("/", CreateSeries)
            .WithName("CreateSeries");

        group.MapPost("/general", EnsureGeneralSeries)
            .WithName("EnsureGeneralSeries");

        group.MapPut("/{id:long}", UpdateSeries)
            .WithName("UpdateSeries");

        group.MapPut("/{id:long}/translations/{culture}", UpsertSeriesTranslation)
            .WithName("UpsertSeriesTranslation");

        group.MapDelete("/{id:long}", DeleteSeries)
            .WithName("DeleteSeries");
    }

    private static async Task<IResult> GetAllSeries(
        [FromServices] IAeroSeriesActor seriesActor,
        [FromServices] IQuerySession query,
        [FromServices] ISiteContext siteContext,
        CancellationToken cancellationToken = default)
    {
        var series = await seriesActor.GetAllAsync(cancellationToken);
        var scoped = series
            .Where(x => x.SiteId == siteContext.SiteId)
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var counts = await GetContentCountsAsync(query, scoped.Select(x => x.Id), siteContext.SiteId, cancellationToken);

        return TypedResults.Ok(scoped
            .Select(x => ToSummary(x, counts.GetValueOrDefault(x.Id)))
            .ToList());
    }

    private static async Task<IResult> GetSeriesById(
        long id,
        [FromServices] IAeroSeriesActor seriesActor,
        [FromServices] IQuerySession query,
        [FromServices] ISiteContext siteContext,
        CancellationToken cancellationToken = default)
    {
        var result = await seriesActor.GetByIdAsync(id, cancellationToken);
        if (!string.IsNullOrWhiteSpace(result.error.Message) || result.data.SiteId != siteContext.SiteId)
            return TypedResults.NotFound(result.error);

        var count = await query.Query<PostDocument>()
            .Where(x => x.SiteId == siteContext.SiteId && x.SeriesId == id).CountAsync(cancellationToken);
        return TypedResults.Ok(ToDetail(result.data, count));
    }

    private static async Task<IResult> CreateSeries(
        [FromBody] HttpCreateSeriesRequest request,
        [FromServices] IValidator<ActorCreateSeriesRequest> validator,
        [FromServices] IAeroSeriesActor seriesActor,
        [FromServices] IQuerySession query,
        [FromServices] ISiteContext siteContext,
        CancellationToken cancellationToken = default)
    {
        var actorRequest = new ActorCreateSeriesRequest(
            siteContext.SiteId,
            request.Name,
            string.IsNullOrWhiteSpace(request.Slug) ? null : request.Slug,
            request.Description);

        var validationResult = await validator.ValidateAsync(actorRequest, cancellationToken);
        if (!validationResult.IsValid)
            return TypedResults.ValidationProblem(validationResult.ToDictionary());

        var result = await seriesActor.CreateAsync(actorRequest, cancellationToken);
        if (!string.IsNullOrWhiteSpace(result.error.Message))
            return TypedResults.BadRequest(result.error);

        return TypedResults.Ok(ToDetail(result.data, await CountSeriesContentAsync(query, siteContext.SiteId, result.data.Id, cancellationToken)));
    }

    private static async Task<IResult> ListSeriesTranslations(
        long id,
        [FromServices] IAeroSeriesActor seriesActor,
        [FromServices] IQuerySession query,
        [FromServices] ISiteContext siteContext,
        CancellationToken cancellationToken = default)
    {
        var result = await seriesActor.GetByIdAsync(id, cancellationToken);
        if (!string.IsNullOrWhiteSpace(result.error.Message) || result.data.SiteId != siteContext.SiteId)
            return TypedResults.NotFound(result.error);

        var site = await query.LoadAsync<SitesModel>(siteContext.SiteId, cancellationToken);
        var defaultCulture = ContentSlugDocument.NormalizeCulture(site?.DefaultCulture ?? SitesModel.DefaultCultureName);
        var supportedCultures = site?.SupportedCultures is { Count: > 0 } cultures
            ? cultures.Select(ContentSlugDocument.NormalizeCulture).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            : [defaultCulture];

        if (!supportedCultures.Contains(defaultCulture, StringComparer.OrdinalIgnoreCase))
            supportedCultures.Insert(0, defaultCulture);

        var translations = await query.Query<SeriesTranslation>()
            .Where(x => x.SeriesId == id)
            .ToListAsync(cancellationToken);
        var translationLookup = translations.ToDictionary(x => x.Culture, StringComparer.OrdinalIgnoreCase);

        var items = supportedCultures
            .Select(culture =>
            {
                var isDefault = string.Equals(culture, defaultCulture, StringComparison.OrdinalIgnoreCase);
                var hasTranslation = translationLookup.TryGetValue(culture, out var translation);
                return isDefault
                    ? new SeriesTranslationSummary(culture, result.data.Name ?? string.Empty, result.data.Slug ?? string.Empty, result.data.Description, true, true)
                    : new SeriesTranslationSummary(
                        culture,
                        translation?.Name ?? string.Empty,
                        translation?.Slug ?? string.Empty,
                        translation?.Description,
                        hasTranslation,
                        false);
            })
            .ToList();

        return TypedResults.Ok(items);
    }

    private static async Task<IResult> EnsureGeneralSeries(
        [FromServices] IAeroSeriesActor seriesActor,
        [FromServices] IQuerySession query,
        [FromServices] ISiteContext siteContext,
        CancellationToken cancellationToken = default)
    {
        var series = await seriesActor.EnsureGeneralAsync(siteContext.SiteId, cancellationToken);
        return TypedResults.Ok(ToDetail(series, await CountSeriesContentAsync(query, siteContext.SiteId, series.Id, cancellationToken)));
    }

    private static async Task<IResult> UpdateSeries(
        long id,
        [FromBody] HttpUpdateSeriesRequest request,
        [FromServices] IValidator<ActorUpdateSeriesRequest> validator,
        [FromServices] IAeroSeriesActor seriesActor,
        [FromServices] IQuerySession query,
        [FromServices] ISiteContext siteContext,
        CancellationToken cancellationToken = default)
    {
        var actorRequest = new ActorUpdateSeriesRequest(
            id,
            request.Name,
            string.IsNullOrWhiteSpace(request.Slug) ? null : request.Slug,
            request.Description);

        var validationResult = await validator.ValidateAsync(actorRequest, cancellationToken);
        if (!validationResult.IsValid)
            return TypedResults.ValidationProblem(validationResult.ToDictionary());

        var existing = await seriesActor.GetByIdAsync(id, cancellationToken);
        if (!string.IsNullOrWhiteSpace(existing.error.Message) || existing.data.SiteId != siteContext.SiteId)
            return TypedResults.NotFound(existing.error);

        var result = await seriesActor.UpdateAsync(actorRequest, cancellationToken);
        if (!string.IsNullOrWhiteSpace(result.error.Message))
            return TypedResults.BadRequest(result.error);

        return TypedResults.Ok(ToDetail(result.data, await CountSeriesContentAsync(query, siteContext.SiteId, id, cancellationToken)));
    }

    private static async Task<IResult> UpsertSeriesTranslation(
        long id,
        string culture,
        [FromBody] UpsertSeriesTranslationRequest request,
        [FromServices] IAeroSeriesActor seriesActor,
        [FromServices] IDocumentSession session,
        [FromServices] ISiteContext siteContext,
        CancellationToken cancellationToken = default)
    {
        var seriesResult = await seriesActor.GetByIdAsync(id, cancellationToken);
        if (!string.IsNullOrWhiteSpace(seriesResult.error.Message) || seriesResult.data.SiteId != siteContext.SiteId)
            return TypedResults.NotFound(seriesResult.error);

        var normalizedCulture = ContentSlugDocument.NormalizeCulture(culture);
        var site = await session.LoadAsync<SitesModel>(siteContext.SiteId, cancellationToken);
        var defaultCulture = ContentSlugDocument.NormalizeCulture(site?.DefaultCulture ?? SitesModel.DefaultCultureName);
        if (string.Equals(normalizedCulture, defaultCulture, StringComparison.OrdinalIgnoreCase))
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Default culture is edited on the series itself",
                Detail = "Edit the base series name and slug instead of creating a translation for the default culture.",
                Status = StatusCodes.Status400BadRequest
            });

        var translation = await session.Query<SeriesTranslation>()
            .FirstOrDefaultAsync(x => x.SeriesId == id && x.Culture == normalizedCulture, cancellationToken);

        if (translation is null)
        {
            translation = new SeriesTranslation
            {
                Id = Snowflake.NewId(),
                SeriesId = id,
                Culture = normalizedCulture
            };
        }

        translation.Name = request.Name;
        translation.Slug = string.IsNullOrWhiteSpace(request.Slug) ? request.Name.ToLowerInvariant().Replace(' ', '-') : request.Slug;
        translation.Description = request.Description;
        translation.ModifiedOn = DateTimeOffset.UtcNow;

        session.Store(translation);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new SeriesTranslationSummary(
            normalizedCulture,
            translation.Name,
            translation.Slug,
            translation.Description,
            true,
            false));
    }

    private static async Task<IResult> DeleteSeries(
        long id,
        [FromServices] IAeroSeriesActor seriesActor,
        [FromServices] IQuerySession query,
        [FromServices] ISiteContext siteContext,
        CancellationToken cancellationToken = default)
    {
        var existing = await seriesActor.GetByIdAsync(id, cancellationToken);
        if (!string.IsNullOrWhiteSpace(existing.error.Message) || existing.data.SiteId != siteContext.SiteId)
            return TypedResults.NotFound(existing.error);

        var contentCount = await CountSeriesContentAsync(query, siteContext.SiteId, id, cancellationToken);
        if (contentCount > 0)
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Series is in use",
                Detail = "Move posts to another series before deleting this series.",
                Status = StatusCodes.Status400BadRequest
            });

        var result = await seriesActor.DeleteAsync(new ActorDeleteSeriesRequest(id), cancellationToken);
        return string.IsNullOrWhiteSpace(result.error.Message)
            ? TypedResults.Ok(true)
            : TypedResults.BadRequest(result.error);
    }

    private static async Task<Dictionary<long, int>> GetContentCountsAsync(
        IQuerySession query,
        IEnumerable<long> seriesIds,
        long siteId,
        CancellationToken cancellationToken)
    {
        var ids = seriesIds.Distinct().ToArray();
        if (ids.Length == 0)
            return [];

        var posts = await query.Query<PostDocument>()
            .Where(x => x.SiteId == siteId && x.SeriesId.HasValue && ids.Contains(x.SeriesId.Value))
            .ToListAsync(cancellationToken);

        return posts
            .GroupBy(x => x.SeriesId!.Value)
            .ToDictionary(x => x.Key, x => x.Count());
    }

    private static Task<int> CountSeriesContentAsync(
        IQuerySession query,
        long siteId,
        long seriesId,
        CancellationToken cancellationToken)
        => query.Query<PostDocument>()
            .Where(x => x.SiteId == siteId && x.SeriesId == seriesId).CountAsync(cancellationToken);

    private static SeriesSummary ToSummary(SeriesViewModel vm, int count)
        => new(vm.Id, vm.Name ?? string.Empty, vm.Slug ?? string.Empty, vm.Description, count);

    private static SeriesDetail ToDetail(SeriesViewModel vm, int count)
        => new(vm.Id, vm.Name ?? string.Empty, vm.Slug ?? string.Empty, vm.Description, count, vm.CreatedOn.DateTime);
}
