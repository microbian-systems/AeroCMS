using Aero.Actors;
using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Core.Entities;
using System.Globalization;
using ActorCreateSeriesRequest = Aero.Cms.Abstractions.Requests.CreateSeriesRequest;
using ActorDeleteSeriesRequest = Aero.Cms.Abstractions.Requests.DeleteSeriesRequest;
using ActorUpdateSeriesRequest = Aero.Cms.Abstractions.Requests.UpdateSeriesRequest;
using IRequest = Aero.Core.Commands.IRequest;

namespace Aero.Cms.Modules.Posts.Grains;

/// <summary>
/// Represents a class for AeroSeriesGrain.
/// </summary>
public sealed class AeroSeriesGrain(
    ILogger<AeroSeriesGrain> log,
    IDocumentStore store)
    : AeroActor(log), IAeroSeriesActor
{
    private SeriesViewModel _state = new();

        /// <summary>
    /// GetStateAsync method.
    /// </summary>
public Task<SeriesViewModel> GetStateAsync(CancellationToken ct) => Task.FromResult(_state);

        /// <summary>
    /// UpdateStateAsync method.
    /// </summary>
public Task UpdateStateAsync(SeriesViewModel state, CancellationToken ct)
    {
        _state = state;
        return Task.CompletedTask;
    }

        /// <summary>
    /// GetByIdAsync method.
    /// </summary>
public async Task<AeroRequestResponse<SeriesViewModel>> GetByIdAsync(long id, CancellationToken ct)
    {
        await using var session = await store.LightweightSessionAsync();
        var series = await session.LoadAsync<Models.Series>(id, ct);
        var translation = series is null
            ? null
            : await LoadTranslationAsync(session, series.Id, GetCurrentCulture(), ct);

        return series is null
            ? NotFound($"Series {id} not found")
            : Ok(PostTaxonomyTranslationMapper.MapSeries(series, translation));
    }

        /// <summary>
    /// GetByIdsAsync method.
    /// </summary>
public async Task<AeroRequestResponse<SeriesViewModel>> GetByIdsAsync(long[] ids, CancellationToken ct)
    {
        await using var session = await store.LightweightSessionAsync();
        var series = await session.Query<Models.Series>()
            .Where(x => ids.Contains(x.Id))
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

        return Ok(series.Select(x => PostTaxonomyTranslationMapper.MapSeries(x)).ToList());
    }

        /// <summary>
    /// CreateAsync method.
    /// </summary>
public async Task<AeroRequestResponse<SeriesViewModel>> CreateAsync(IRequest request, CancellationToken ct)
    {
        if (request is not ActorCreateSeriesRequest create)
            return Fail("Expected CreateSeriesRequest");

        await using var session = await store.LightweightSessionAsync();
        var series = new Models.Series
        {
            Id = Snowflake.NewId(),
            SiteId = create.siteId,
            Name = create.Name,
            Slug = create.Slug ?? GenerateSlug(create.Name),
            Description = create.Description
        };

        session.Store(series);
        await session.SaveChangesAsync(ct);

        return Ok(PostTaxonomyTranslationMapper.MapSeries(series));
    }

        /// <summary>
    /// UpdateAsync method.
    /// </summary>
public async Task<AeroRequestResponse<SeriesViewModel>> UpdateAsync(IRequest request, CancellationToken ct)
    {
        if (request is not ActorUpdateSeriesRequest update)
            return Fail("Expected UpdateSeriesRequest");

        await using var session = await store.LightweightSessionAsync();
        var series = await session.LoadAsync<Models.Series>(update.Id, ct);
        if (series is null)
            return NotFound($"Series {update.Id} not found");

        series.Name = update.Name;
        series.Slug = update.Slug ?? GenerateSlug(update.Name);
        series.Description = update.Description;
        series.ModifiedOn = DateTimeOffset.UtcNow;

        session.Store(series);
        await session.SaveChangesAsync(ct);

        return Ok(PostTaxonomyTranslationMapper.MapSeries(series));
    }

        /// <summary>
    /// DeleteAsync method.
    /// </summary>
public async Task<AeroRequestResponse<SeriesViewModel>> DeleteAsync(IRequest request, CancellationToken ct)
    {
        if (request is not ActorDeleteSeriesRequest delete)
            return Fail("Expected DeleteSeriesRequest");

        await using var session = await store.LightweightSessionAsync();
        var series = await session.LoadAsync<Models.Series>(delete.Id, ct);
        if (series is null)
            return NotFound($"Series {delete.Id} not found");

        session.Delete(series);
        await session.SaveChangesAsync(ct);

        return Ok(PostTaxonomyTranslationMapper.MapSeries(series));
    }

        /// <summary>
    /// GetBySiteIdAsync method.
    /// </summary>
public async Task<AeroRequestResponse<SeriesViewModel>> GetBySiteIdAsync(long siteId, int page = 1, int rows = 10, CancellationToken ct = default)
    {
        await using var session = await store.LightweightSessionAsync();
        var series = await session.Query<Models.Series>()
            .Where(x => x.SiteId == siteId)
            .OrderBy(x => x.Name)
            .Skip((page - 1) * rows)
            .Take(rows)
            .ToListAsync(ct);

        return Ok(series.Select(x => PostTaxonomyTranslationMapper.MapSeries(x)).ToList());
    }

        /// <summary>
    /// GetBySlugAsync method.
    /// </summary>
public async Task<AeroRequestResponse<SeriesViewModel>> GetBySlugAsync(long siteId, string slug, CancellationToken ct)
    {
        await using var session = await store.LightweightSessionAsync();
        var series = await session.Query<Models.Series>()
            .Where(x => x.SiteId == siteId && x.Slug == slug)
            .FirstOrDefaultAsync(ct);

        return series is null
            ? NotFound($"Series {slug} not found")
            : Ok(PostTaxonomyTranslationMapper.MapSeries(series));
    }

    Task<AeroRequestResponse<SeriesViewModel>> ICanFindBySlug<SeriesViewModel, string>.GetBySlugAsync(string siteId, string slug, CancellationToken ct)
        => long.TryParse(siteId, out var id)
            ? GetBySlugAsync(id, slug, ct)
            : Task.FromResult(Fail($"Invalid site ID: {siteId}"));

        /// <summary>
    /// GetAllAsync method.
    /// </summary>
public async Task<List<SeriesViewModel>> GetAllAsync(CancellationToken ct = default)
    {
        await using var session = await store.LightweightSessionAsync();
        var series = await session.Query<Models.Series>()
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

        return series.Select(x => PostTaxonomyTranslationMapper.MapSeries(x)).ToList();
    }

        /// <summary>
    /// EnsureGeneralAsync method.
    /// </summary>
public async Task<SeriesViewModel> EnsureGeneralAsync(long siteId, CancellationToken ct = default)
    {
        await using var session = await store.LightweightSessionAsync();
        var general = await session.Query<Models.Series>()
            .Where(x => x.SiteId == siteId && x.Slug == "general")
            .FirstOrDefaultAsync(ct);

        if (general is null)
        {
            general = new Models.Series
            {
                Id = Snowflake.NewId(),
                SiteId = siteId,
                Name = "General",
                Slug = "general",
                Description = "Default blog series"
            };
            session.Store(general);
            await session.SaveChangesAsync(ct);
        }

        return PostTaxonomyTranslationMapper.MapSeries(general);
    }

    private static string GenerateSlug(string name) =>
        name.ToLowerInvariant().Replace(' ', '-').Replace("--", "-");

    private static AeroRequestResponse<SeriesViewModel> Ok(SeriesViewModel vm)
        => new(vm, new SeriesErrorViewModel());

    private static AeroRequestResponse<SeriesViewModel> Ok(IReadOnlyList<SeriesViewModel> list)
    {
        var primary = list.Count > 0 ? list[0] : new SeriesViewModel();
        return new AeroRequestResponse<SeriesViewModel>(primary, new SeriesErrorViewModel());
    }

    private static AeroRequestResponse<SeriesViewModel> NotFound(string msg)
        => new(new SeriesViewModel(), new SeriesErrorViewModel { Message = msg });

    private static AeroRequestResponse<SeriesViewModel> Fail(string msg)
        => new(new SeriesViewModel(), new SeriesErrorViewModel { Message = msg });

    private static Task<SeriesTranslation?> LoadTranslationAsync(IDocumentSession session, long seriesId, string culture, CancellationToken ct)
    {
        if (string.Equals(culture, SitesModel.DefaultCultureName, StringComparison.OrdinalIgnoreCase))
            return Task.FromResult<SeriesTranslation?>(null);

        return session.Query<SeriesTranslation>()
            .Where(x => x.SeriesId == seriesId && x.Culture == culture)
            .FirstOrDefaultAsync(ct);
    }

    private static string GetCurrentCulture()
    {
        try
        {
            return CultureInfo.GetCultureInfo(CultureInfo.CurrentUICulture.Name).Name;
        }
        catch (CultureNotFoundException)
        {
            return SitesModel.DefaultCultureName;
        }
    }
}
