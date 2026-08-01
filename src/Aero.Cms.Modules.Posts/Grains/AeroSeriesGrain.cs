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
/// Implements series actor operations over short-lived Sable sessions.
/// </summary>
/// <param name="log">The actor logger.</param>
/// <param name="store">The store used to open a session for each operation.</param>
/// <remarks>
/// The in-memory state methods are activation-local and independent of persisted series documents.
/// Unlike category and tag mutations, series mutations do not publish integration events.
/// </remarks>
public sealed class AeroSeriesGrain(
    ILogger<AeroSeriesGrain> log,
    IDocumentStore store)
    : AeroActor(log), IAeroSeriesActor
{
    private SeriesViewModel _state = new();

    /// <summary>
    /// Returns the current activation-local state without reading persistence.
    /// </summary>
    /// <param name="ct">A cancellation token that is not observed because the operation is synchronous.</param>
    /// <returns>The current state reference.</returns>
public Task<SeriesViewModel> GetStateAsync(CancellationToken ct) => Task.FromResult(_state);

    /// <summary>
    /// Replaces the activation-local state without persisting it.
    /// </summary>
    /// <param name="state">The state reference to retain.</param>
    /// <param name="ct">A cancellation token that is not observed because the operation is synchronous.</param>
    /// <returns>A completed task.</returns>
public Task UpdateStateAsync(SeriesViewModel state, CancellationToken ct)
    {
        _state = state;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Loads a series by identifier and overlays the current UI-culture translation when available.
    /// </summary>
    /// <param name="id">The persisted series identifier.</param>
    /// <param name="ct">A token used to cancel persistence queries.</param>
    /// <returns>The mapped series, or an error response when no document exists.</returns>
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
    /// Loads base series documents whose identifiers are in the supplied array.
    /// </summary>
    /// <param name="ids">The series identifiers to query.</param>
    /// <param name="ct">A token used to cancel persistence queries.</param>
    /// <returns>The first mapped match, or an empty view model when none match.</returns>
    /// <remarks>This method does not apply culture translations.</remarks>
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
    /// Creates and commits a series for a recognized create request.
    /// </summary>
    /// <param name="request">A series create request; other request types produce a failure response.</param>
    /// <param name="ct">A token used for the database commit.</param>
    /// <returns>The created series or a request-type failure response.</returns>
    /// <remarks>The generated identifier makes repeated calls create distinct documents.</remarks>
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
    /// Replaces the mutable fields of an existing series and commits the document.
    /// </summary>
    /// <param name="request">A series update request; other request types produce a failure response.</param>
    /// <param name="ct">A token used for persistence.</param>
    /// <returns>The updated series, a not-found response, or a request-type failure response.</returns>
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
    /// Deletes an existing series.
    /// </summary>
    /// <param name="request">A series delete request; other request types produce a failure response.</param>
    /// <param name="ct">A token used for persistence.</param>
    /// <returns>The deleted series, a not-found response, or a request-type failure response.</returns>
    /// <remarks>This actor does not check whether posts still reference the series.</remarks>
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
    /// Returns the first base series from a name-ordered page for one site.
    /// </summary>
    /// <param name="siteId">The owning site identifier.</param>
    /// <param name="page">The one-based page number used to calculate the query offset.</param>
    /// <param name="rows">The maximum number of rows queried.</param>
    /// <param name="ct">A token used to cancel persistence queries.</param>
    /// <returns>The first mapped series in the page, or an empty view model when the page is empty.</returns>
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
    /// Finds a base series by exact slug within one site.
    /// </summary>
    /// <param name="siteId">The owning site identifier.</param>
    /// <param name="slug">The exact base slug.</param>
    /// <param name="ct">A token used to cancel persistence queries.</param>
    /// <returns>The mapped base series or a not-found response.</returns>
    /// <remarks>This lookup does not inspect translated slugs or overlay translated fields.</remarks>
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

    /// <summary>
    /// Adapts the string site-key contract to the numeric site identifier used by persistence.
    /// </summary>
    Task<AeroRequestResponse<SeriesViewModel>> ICanFindBySlug<SeriesViewModel, string>.GetBySlugAsync(string siteId, string slug, CancellationToken ct)
        => long.TryParse(siteId, out var id)
            ? GetBySlugAsync(id, slug, ct)
            : Task.FromResult(Fail($"Invalid site ID: {siteId}"));

    /// <summary>
    /// Returns every base series across all sites ordered by name.
    /// </summary>
    /// <param name="ct">A token used to cancel persistence queries.</param>
    /// <returns>All mapped series; callers that require tenant isolation must filter by <c>SiteId</c>.</returns>
public async Task<List<SeriesViewModel>> GetAllAsync(CancellationToken ct = default)
    {
        await using var session = await store.LightweightSessionAsync();
        var series = await session.Query<Models.Series>()
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

        return series.Select(x => PostTaxonomyTranslationMapper.MapSeries(x)).ToList();
    }

    /// <summary>
    /// Gets or creates the site's default <c>general</c> series.
    /// </summary>
    /// <param name="siteId">The owning site identifier.</param>
    /// <param name="ct">A token used for the lookup and optional commit.</param>
    /// <returns>The existing or newly persisted General series.</returns>
    /// <remarks>The lookup followed by insert is not expressed as a single database upsert.</remarks>
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

    /// <summary>
    /// Produces the grain's simple lowercase, space-to-hyphen fallback slug.
    /// </summary>
    private static string GenerateSlug(string name) =>
        name.ToLowerInvariant().Replace(' ', '-').Replace("--", "-");

    /// <summary>
    /// Creates a successful response around one series.
    /// </summary>
    private static AeroRequestResponse<SeriesViewModel> Ok(SeriesViewModel vm)
        => new(vm, new SeriesErrorViewModel());

    /// <summary>
    /// Adapts a list to the single-data response contract by selecting its first item.
    /// </summary>
    private static AeroRequestResponse<SeriesViewModel> Ok(IReadOnlyList<SeriesViewModel> list)
    {
        var primary = list.Count > 0 ? list[0] : new SeriesViewModel();
        return new AeroRequestResponse<SeriesViewModel>(primary, new SeriesErrorViewModel());
    }

    /// <summary>
    /// Creates an error response with an empty series payload.
    /// </summary>
    private static AeroRequestResponse<SeriesViewModel> NotFound(string msg)
        => new(new SeriesViewModel(), new SeriesErrorViewModel { Message = msg });

    /// <summary>
    /// Creates a request failure response with an empty series payload.
    /// </summary>
    private static AeroRequestResponse<SeriesViewModel> Fail(string msg)
        => new(new SeriesViewModel(), new SeriesErrorViewModel { Message = msg });

    /// <summary>
    /// Loads the requested series translation unless the culture is the CMS default.
    /// </summary>
    private static Task<SeriesTranslation?> LoadTranslationAsync(IDocumentSession session, long seriesId, string culture, CancellationToken ct)
    {
        if (string.Equals(culture, SitesModel.DefaultCultureName, StringComparison.OrdinalIgnoreCase))
            return Task.FromResult<SeriesTranslation?>(null);

        return session.Query<SeriesTranslation>()
            .Where(x => x.SeriesId == seriesId && x.Culture == culture)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Returns the canonical current UI culture name, falling back to the configured CMS default name.
    /// </summary>
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
