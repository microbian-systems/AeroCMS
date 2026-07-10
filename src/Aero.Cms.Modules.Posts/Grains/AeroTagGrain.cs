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
/// Orleans grain for tag management — wraps AeroDB persistence behind
/// <see cref="IAeroTagActor"/>. Publishes Wolverine events after mutations.
/// </summary>
public sealed class AeroTagGrain : AeroActor, IAeroTagActor
{
    private readonly IDocumentStore _store;
    private readonly IMessageBus _bus;
    private TagViewModel _state = new();

        /// <summary>
    /// Initializes a new instance of the <see cref="AeroTagGrain"/> class.
    /// </summary>
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
    /// GetStateAsync method.
    /// </summary>
public Task<TagViewModel> GetStateAsync(CancellationToken ct)
        => Task.FromResult(_state);

        /// <summary>
    /// UpdateStateAsync method.
    /// </summary>
public Task UpdateStateAsync(TagViewModel state, CancellationToken ct)
    {
        _state = state;
        return Task.CompletedTask;
    }

    // ── ICruddable<TagViewModel, long> ────────────────────────────────

        /// <summary>
    /// GetByIdAsync method.
    /// </summary>
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
    /// GetByIdsAsync method.
    /// </summary>
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
    /// CreateAsync method.
    /// </summary>
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
    /// UpdateAsync method.
    /// </summary>
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
    /// DeleteAsync method.
    /// </summary>
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
    /// GetBySiteIdAsync method.
    /// </summary>
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
    /// GetBySlugAsync method.
    /// </summary>
public Task<AeroRequestResponse<TagViewModel>> GetBySlugAsync(long siteId, string slug, CancellationToken ct)
        => GetBySlugCoreAsync(siteId, slug, ct);

    Task<AeroRequestResponse<TagViewModel>> ICanFindBySlug<TagViewModel, string>.GetBySlugAsync(string siteId, string slug, CancellationToken ct)
    {
        if (long.TryParse(siteId, out var id))
            return GetBySlugCoreAsync(id, slug, ct);
        return Task.FromResult(Fail($"Invalid site ID: {siteId}"));
    }

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
    /// GetAllAsync method.
    /// </summary>
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

    private static string GenerateSlug(string name) =>
        name.ToLowerInvariant().Replace(' ', '-').Replace("--", "-");

    private static AeroRequestResponse<TagViewModel> Ok(TagViewModel vm)
        => new(vm, new TagErrorViewModel());

    private static AeroRequestResponse<TagViewModel> Ok(IReadOnlyList<TagViewModel> list)
    {
        var primary = list.Count > 0 ? list[0] : new TagViewModel();
        return new AeroRequestResponse<TagViewModel>(primary, new TagErrorViewModel());
    }

    private static AeroRequestResponse<TagViewModel> NotFound(string msg)
        => new(new TagViewModel(), new TagErrorViewModel { Message = msg });

    private static AeroRequestResponse<TagViewModel> Fail(string msg)
        => new(new TagViewModel(), new TagErrorViewModel { Message = msg });

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
