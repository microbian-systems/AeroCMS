using Aero.Actors;
using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Events;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Core.Entities;
using System.Globalization;
using Wolverine;
using IRequest = Aero.Core.Commands.IRequest;

namespace Aero.Cms.Modules.Posts.Grains;

/// <summary>
/// Implements tag actor operations over short-lived Sable sessions.
/// </summary>
/// <remarks>
/// The in-memory state methods are activation-local and independent of persisted tag documents.
/// Mutations commit before publishing their Wolverine event, so persistence and notification are not
/// atomic; a publish failure can be observed after the database change has succeeded.
/// </remarks>
public sealed class AeroTagGrain : AeroActor, IAeroTagActor
{
    private readonly IDocumentStore _store;
    private readonly IMessageBus _bus;
    private TagViewModel _state = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="AeroTagGrain"/> class.
    /// </summary>
    /// <param name="log">The actor logger.</param>
    /// <param name="store">The store used to open a session for each operation.</param>
    /// <param name="bus">The bus that receives post-commit tag events.</param>
public AeroTagGrain(
        ILogger<AeroActor> log,
        IDocumentStore store,
        IMessageBus bus)
        : base(log)
    {
        _store = store;
        _bus = bus;
    }

    // ── IHaveState<TagViewModel> ──────────────────────────────────────

    /// <summary>
    /// Returns the current activation-local state without reading persistence.
    /// </summary>
    /// <param name="ct">A cancellation token that is not observed because the operation is synchronous.</param>
    /// <returns>The current state reference.</returns>
public Task<TagViewModel> GetStateAsync(CancellationToken ct)
        => Task.FromResult(_state);

    /// <summary>
    /// Replaces the activation-local state without persisting or publishing it.
    /// </summary>
    /// <param name="state">The state reference to retain.</param>
    /// <param name="ct">A cancellation token that is not observed because the operation is synchronous.</param>
    /// <returns>A completed task.</returns>
public Task UpdateStateAsync(TagViewModel state, CancellationToken ct)
    {
        _state = state;
        return Task.CompletedTask;
    }

    // ── ICruddable<TagViewModel, long> ────────────────────────────────

    /// <summary>
    /// Loads a tag by identifier and overlays the current UI-culture translation when available.
    /// </summary>
    /// <param name="id">The persisted tag identifier.</param>
    /// <param name="ct">A token used to cancel persistence queries.</param>
    /// <returns>The mapped tag, or an error response when no document exists.</returns>
public async Task<AeroRequestResponse<TagViewModel>> GetByIdAsync(long id, CancellationToken ct)
    {
        await using var session = await _store.LightweightSessionAsync();
        var tag = await session.LoadAsync<Models.Tag>(id, ct);
        var translations = tag is null
            ? new Dictionary<long, TagTranslation>()
            : await LoadTranslationsAsync(session, [tag.Id], GetCurrentCulture(), ct);

        return tag is not null
            ? Ok(PostTaxonomyTranslationMapper.MapTag(tag, translations.GetValueOrDefault(tag.Id)))
            : NotFound($"Tag {id} not found");
    }

    /// <summary>
    /// Loads tags whose identifiers are in the supplied array.
    /// </summary>
    /// <param name="ids">The tag identifiers to query.</param>
    /// <param name="ct">A token used to cancel persistence queries.</param>
    /// <returns>The first mapped match, or an empty view model when none match.</returns>
public async Task<AeroRequestResponse<TagViewModel>> GetByIdsAsync(long[] ids, CancellationToken ct)
    {
        await using var session = await _store.LightweightSessionAsync();
        var tags = await session.Query<Models.Tag>()
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(ct);

        var translations = await LoadTranslationsAsync(session, tags.Select(x => x.Id), GetCurrentCulture(), ct);
        var results = tags.Select(tag => PostTaxonomyTranslationMapper.MapTag(tag, translations.GetValueOrDefault(tag.Id))).ToList();
        return Ok(results);
    }

    /// <summary>
    /// Creates and commits a tag for a recognized create request, then publishes a created event.
    /// </summary>
    /// <param name="request">A <see cref="CreateTagRequest"/>; other request types produce a failure response.</param>
    /// <param name="ct">A token used for the database commit.</param>
    /// <returns>The created tag or a request-type failure response.</returns>
    /// <remarks>The generated identifier makes repeated calls create distinct documents.</remarks>
public async Task<AeroRequestResponse<TagViewModel>> CreateAsync(IRequest request, CancellationToken ct)
    {
        if (request is not CreateTagRequest create)
            return Fail("Expected CreateTagRequest");

        await using var session = await _store.LightweightSessionAsync();

        var tag = new Models.Tag
        {
            Id = Snowflake.NewId(),
            SiteId = create.siteId,
            Name = create.Name,
            Slug = create.Slug ?? GenerateSlug(create.Name),
        };

        session.Store(tag);
        await session.SaveChangesAsync(ct);

        await _bus.PublishAsync(new TagViewModelCreated(PostTaxonomyTranslationMapper.MapTag(tag)));

        return Ok(PostTaxonomyTranslationMapper.MapTag(tag));
    }

    /// <summary>
    /// Replaces the mutable fields of an existing tag and publishes an updated event after commit.
    /// </summary>
    /// <param name="request">An <see cref="UpdateTagRequest"/>; other request types produce a failure response.</param>
    /// <param name="ct">A token used for persistence.</param>
    /// <returns>The updated tag, a not-found response, or a request-type failure response.</returns>
public async Task<AeroRequestResponse<TagViewModel>> UpdateAsync(IRequest request, CancellationToken ct)
    {
        if (request is not UpdateTagRequest update)
            return Fail("Expected UpdateTagRequest");

        await using var session = await _store.LightweightSessionAsync();
        var tag = await session.LoadAsync<Models.Tag>(update.Id, ct);

        if (tag is null)
            return NotFound($"Tag {update.Id} not found");

        tag.Name = update.Name;
        tag.Slug = update.Slug ?? GenerateSlug(update.Name);
        tag.ModifiedOn = DateTimeOffset.UtcNow;

        session.Store(tag);
        await session.SaveChangesAsync(ct);

        await _bus.PublishAsync(new TagViewModelUpdated(PostTaxonomyTranslationMapper.MapTag(tag)));

        return Ok(PostTaxonomyTranslationMapper.MapTag(tag));
    }

    /// <summary>
    /// Deletes an existing tag and publishes a deleted event after commit.
    /// </summary>
    /// <param name="request">A <see cref="DeleteTagRequest"/>; other request types produce a failure response.</param>
    /// <param name="ct">A token used for persistence.</param>
    /// <returns>The deleted tag, a not-found response, or a request-type failure response.</returns>
    /// <remarks>This actor does not check whether posts still reference the tag.</remarks>
public async Task<AeroRequestResponse<TagViewModel>> DeleteAsync(IRequest request, CancellationToken ct)
    {
        if (request is not DeleteTagRequest delete)
            return Fail("Expected DeleteTagRequest");

        await using var session = await _store.LightweightSessionAsync();
        var tag = await session.LoadAsync<Models.Tag>(delete.Id, ct);

        if (tag is null)
            return NotFound($"Tag {delete.Id} not found");

        session.Delete(tag);
        await session.SaveChangesAsync(ct);

        await _bus.PublishAsync(new TagViewModelDeleted(PostTaxonomyTranslationMapper.MapTag(tag)));

        return Ok(PostTaxonomyTranslationMapper.MapTag(tag));
    }

    // ── ICanFindBySite<TagViewModel, long> ────────────────────────────

    /// <summary>
    /// Returns the first tag from a name-ordered page for one site.
    /// </summary>
    /// <param name="siteId">The owning site identifier.</param>
    /// <param name="page">The one-based page number used to calculate the query offset.</param>
    /// <param name="rows">The maximum number of rows queried.</param>
    /// <param name="ct">A token used to cancel persistence queries.</param>
    /// <returns>The first mapped tag in the page, or an empty view model when the page is empty.</returns>
public async Task<AeroRequestResponse<TagViewModel>> GetBySiteIdAsync(
        long siteId,
        int page = 1,
        int rows = 10,
        CancellationToken ct = default)
    {
        await using var session = await _store.LightweightSessionAsync();
        var tags = await session.Query<Models.Tag>()
            .Where(x => x.SiteId == siteId)
            .OrderBy(x => x.Name)
            .Skip((page - 1) * rows)
            .Take(rows)
            .ToListAsync(ct);

        var translations = await LoadTranslationsAsync(session, tags.Select(x => x.Id), GetCurrentCulture(), ct);
        var results = tags.Select(tag => PostTaxonomyTranslationMapper.MapTag(tag, translations.GetValueOrDefault(tag.Id))).ToList();
        return Ok(results);
    }

    // ── ICanFindBySlug ────────────────────────────────────────────────

    /// <summary>
    /// Finds the first tag for a site by its base slug and overlays the current-culture display fields.
    /// </summary>
    /// <param name="siteId">The owning site identifier.</param>
    /// <param name="slug">The exact base slug to match.</param>
    /// <param name="ct">A token used to cancel persistence queries.</param>
    /// <returns>The first match, or an empty successful response when no tag matches.</returns>
public Task<AeroRequestResponse<TagViewModel>> GetBySlugAsync(long siteId, string slug, CancellationToken ct)
        => GetBySlugCoreAsync(siteId, slug, ct);

    /// <summary>
    /// Adapts the string site-key contract to the numeric site identifier used by persistence.
    /// </summary>
    Task<AeroRequestResponse<TagViewModel>> ICanFindBySlug<TagViewModel, string>.GetBySlugAsync(string siteId, string slug, CancellationToken ct)
    {
        if (long.TryParse(siteId, out var id))
            return GetBySlugCoreAsync(id, slug, ct);
        return Task.FromResult(Fail($"Invalid site ID: {siteId}"));
    }

    /// <summary>
    /// Queries a base slug within one site and applies the current-culture translation to display fields.
    /// </summary>
    private async Task<AeroRequestResponse<TagViewModel>> GetBySlugCoreAsync(long siteId, string slug, CancellationToken ct)
    {
        await using var session = await _store.LightweightSessionAsync();
        var tags = await session.Query<Models.Tag>()
            .Where(x => x.SiteId == siteId && x.Slug == slug)
            .ToListAsync(ct);

        var translations = await LoadTranslationsAsync(session, tags.Select(x => x.Id), GetCurrentCulture(), ct);
        var results = tags.Select(tag => PostTaxonomyTranslationMapper.MapTag(tag, translations.GetValueOrDefault(tag.Id))).ToList();
        return Ok(results);
    }

    // ── IAeroTagActor.GetAllAsync ─────────────────────────────────────

    /// <summary>
    /// Returns every tag across all sites, ordered by base name and overlaid for the current UI culture.
    /// </summary>
    /// <param name="ct">A token used to cancel persistence queries.</param>
    /// <returns>All mapped tags; callers that require tenant isolation must filter by <c>SiteId</c>.</returns>
public async Task<List<TagViewModel>> GetAllAsync(CancellationToken ct = default)
    {
        await using var session = await _store.LightweightSessionAsync();
        var tags = await session.Query<Models.Tag>()
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

        var translations = await LoadTranslationsAsync(session, tags.Select(x => x.Id), GetCurrentCulture(), ct);
        return tags.Select(tag => PostTaxonomyTranslationMapper.MapTag(tag, translations.GetValueOrDefault(tag.Id))).ToList();
    }

    // ── Helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Produces the grain's simple lowercase, space-to-hyphen fallback slug.
    /// </summary>
    private static string GenerateSlug(string name) =>
        name.ToLowerInvariant().Replace(' ', '-').Replace("--", "-");

    /// <summary>
    /// Creates a successful response around one tag.
    /// </summary>
    private static AeroRequestResponse<TagViewModel> Ok(TagViewModel vm)
        => new(vm, new TagErrorViewModel());

    /// <summary>
    /// Adapts a list to the single-data response contract by selecting its first item.
    /// </summary>
    private static AeroRequestResponse<TagViewModel> Ok(IReadOnlyList<TagViewModel> list)
    {
        var primary = list.Count > 0 ? list[0] : new TagViewModel();
        return new AeroRequestResponse<TagViewModel>(primary, new TagErrorViewModel());
    }

    /// <summary>
    /// Creates an error response with an empty tag payload.
    /// </summary>
    private static AeroRequestResponse<TagViewModel> NotFound(string msg)
        => new(new TagViewModel(), new TagErrorViewModel { Message = msg });

    /// <summary>
    /// Creates a request failure response with an empty tag payload.
    /// </summary>
    private static AeroRequestResponse<TagViewModel> Fail(string msg)
        => new(new TagViewModel(), new TagErrorViewModel { Message = msg });

    /// <summary>
    /// Loads at most one translation per tag for a non-default culture.
    /// </summary>
    private static async Task<IReadOnlyDictionary<long, TagTranslation>> LoadTranslationsAsync(
        IDocumentSession session,
        IEnumerable<long> tagIds,
        string culture,
        CancellationToken ct)
    {
        var ids = tagIds.Distinct().ToArray();
        if (ids.Length == 0 || string.Equals(culture, SitesModel.DefaultCultureName, StringComparison.OrdinalIgnoreCase))
            return new Dictionary<long, TagTranslation>();

        var translations = await session.Query<TagTranslation>()
            .Where(x => x.Culture == culture && ids.Contains(x.TagId))
            .ToListAsync(ct);

        return translations
            .GroupBy(x => x.TagId)
            .ToDictionary(x => x.Key, x => x.First());
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
