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
/// Orleans grain for category management — wraps Marten persistence behind
/// <see cref="IAeroCategoryActor"/>. Publishes Wolverine events after mutations.
/// </summary>
public sealed class AeroCategoryGrain : AeroActor, IAeroCategoryActor
{
    private readonly IDocumentStore _store;
    private readonly IMessageBus _bus;
    private CategoryViewModel _state = new();

    public AeroCategoryGrain(
        ILogger<AeroCategoryGrain> log,
        IDocumentStore store,
        IMessageBus bus)
        : base(log)
    {
        _store = store;
        _bus = bus;
    }

    // ── IHaveState<CategoryViewModel> ──────────────────────────────────

    public Task<CategoryViewModel> GetStateAsync(CancellationToken ct)
        => Task.FromResult(_state);

    public Task UpdateStateAsync(CategoryViewModel state, CancellationToken ct)
    {
        _state = state;
        return Task.CompletedTask;
    }

    // ── ICruddable<CategoryViewModel, long> ────────────────────────────

    public async Task<AeroRequestResponse<CategoryViewModel>> GetByIdAsync(long id, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();
        var category = await session.LoadAsync<Models.Category>(id, ct);
        var translations = category is null
            ? new Dictionary<long, CategoryTranslation>()
            : await LoadTranslationsAsync(session, [category.Id], GetCurrentCulture(), ct);

        return category is not null
            ? Ok(PostTaxonomyTranslationMapper.MapCategory(category, translations.GetValueOrDefault(category.Id)))
            : NotFound($"Category {id} not found");
    }

    public async Task<AeroRequestResponse<CategoryViewModel>> GetByIdsAsync(long[] ids, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();
        var categories = await session.Query<Models.Category>()
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(ct);

        var translations = await LoadTranslationsAsync(session, categories.Select(x => x.Id), GetCurrentCulture(), ct);
        var results = categories.Select(category => PostTaxonomyTranslationMapper.MapCategory(category, translations.GetValueOrDefault(category.Id))).ToList();
        return Ok(results);
    }

    public async Task<AeroRequestResponse<CategoryViewModel>> CreateAsync(IRequest request, CancellationToken ct)
    {
        if (request is not CreateCategoryRequest create)
            return Fail("Expected CreateCategoryRequest");

        await using var session = _store.LightweightSession();

        var category = new Models.Category
        {
            Id = Snowflake.NewId(),
            SiteId = create.siteId,
            Name = create.Name,
            Slug = create.Slug ?? GenerateSlug(create.Name),
            Description = create.Description
        };

        session.Store(category);
        await session.SaveChangesAsync(ct);

        await _bus.PublishAsync(new CategoryViewModelCreated(PostTaxonomyTranslationMapper.MapCategory(category)));

        return Ok(PostTaxonomyTranslationMapper.MapCategory(category));
    }

    public async Task<AeroRequestResponse<CategoryViewModel>> UpdateAsync(IRequest request, CancellationToken ct)
    {
        if (request is not UpdateCategoryRequest update)
            return Fail("Expected UpdateCategoryRequest");

        await using var session = _store.LightweightSession();
        var category = await session.LoadAsync<Models.Category>(update.Id, ct);

        if (category is null)
            return NotFound($"Category {update.Id} not found");

        category.Name = update.Name;
        category.Slug = update.Slug ?? GenerateSlug(update.Name);
        category.Description = update.Description;
        category.ModifiedOn = DateTimeOffset.UtcNow;

        session.Store(category);
        await session.SaveChangesAsync(ct);

        await _bus.PublishAsync(new CategoryViewModelUpdated(PostTaxonomyTranslationMapper.MapCategory(category)));

        return Ok(PostTaxonomyTranslationMapper.MapCategory(category));
    }

    public async Task<AeroRequestResponse<CategoryViewModel>> DeleteAsync(IRequest request, CancellationToken ct)
    {
        if (request is not DeleteCategoryRequest delete)
            return Fail("Expected DeleteCategoryRequest");

        await using var session = _store.LightweightSession();
        var category = await session.LoadAsync<Models.Category>(delete.Id, ct);

        if (category is null)
            return NotFound($"Category {delete.Id} not found");

        session.Delete(category);
        await session.SaveChangesAsync(ct);

        await _bus.PublishAsync(new CategoryViewModelDeleted(PostTaxonomyTranslationMapper.MapCategory(category)));

        return Ok(PostTaxonomyTranslationMapper.MapCategory(category));
    }

    // ── ICanFindBySite<CategoryViewModel, long> ────────────────────────

    public async Task<AeroRequestResponse<CategoryViewModel>> GetBySiteIdAsync(
        long siteId,
        int page = 1,
        int rows = 10,
        CancellationToken ct = default)
    {
        await using var session = _store.LightweightSession();
        var categories = await session.Query<Models.Category>()
            .Where(x => x.SiteId == siteId)
            .OrderBy(x => x.Name)
            .Skip((page - 1) * rows)
            .Take(rows)
            .ToListAsync(ct);

        var translations = await LoadTranslationsAsync(session, categories.Select(x => x.Id), GetCurrentCulture(), ct);
        var results = categories.Select(category => PostTaxonomyTranslationMapper.MapCategory(category, translations.GetValueOrDefault(category.Id))).ToList();
        return Ok(results);
    }

    // ── ICanFindBySlug ────────────────────────────────────────────────

    public Task<AeroRequestResponse<CategoryViewModel>> GetBySlugAsync(long siteId, string slug, CancellationToken ct)
        => GetBySlugCoreAsync(siteId, slug, ct);

    Task<AeroRequestResponse<CategoryViewModel>> ICanFindBySlug<CategoryViewModel, string>.GetBySlugAsync(string siteId, string slug, CancellationToken ct)
    {
        if (long.TryParse(siteId, out var id))
            return GetBySlugCoreAsync(id, slug, ct);
        return Task.FromResult(Fail($"Invalid site ID: {siteId}"));
    }

    private async Task<AeroRequestResponse<CategoryViewModel>> GetBySlugCoreAsync(long siteId, string slug, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();
        var culture = GetCurrentCulture();
        var translatedCategoryIds = await session.Query<CategoryTranslation>()
            .Where(x => x.Culture == culture && x.Slug == slug)
            .Select(x => x.CategoryId)
            .ToListAsync(ct);

        var categories = await session.Query<Models.Category>()
            .Where(x => x.SiteId == siteId && (x.Slug == slug || translatedCategoryIds.Contains(x.Id)))
            .ToListAsync(ct);

        var translations = await LoadTranslationsAsync(session, categories.Select(x => x.Id), culture, ct);
        var results = categories.Select(category => PostTaxonomyTranslationMapper.MapCategory(category, translations.GetValueOrDefault(category.Id))).ToList();
        return Ok(results);
    }

    // ── IAeroCategoryActor.GetAllAsync ─────────────────────────────────

    public async Task<List<CategoryViewModel>> GetAllAsync(CancellationToken ct = default)
    {
        await using var session = _store.LightweightSession();
        var categories = await session.Query<Models.Category>()
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

        var translations = await LoadTranslationsAsync(session, categories.Select(x => x.Id), GetCurrentCulture(), ct);
        return categories.Select(category => PostTaxonomyTranslationMapper.MapCategory(category, translations.GetValueOrDefault(category.Id))).ToList();
    }

    // ── Helpers ────────────────────────────────────────────────────────

    private static string GenerateSlug(string name) =>
        name.ToLowerInvariant().Replace(' ', '-').Replace("--", "-");

    private static AeroRequestResponse<CategoryViewModel> Ok(CategoryViewModel vm)
        => new(vm, new CategoryErrorViewModel());

    private static AeroRequestResponse<CategoryViewModel> Ok(IReadOnlyList<CategoryViewModel> list)
    {
        var primary = list.Count > 0 ? list[0] : new CategoryViewModel();
        return new AeroRequestResponse<CategoryViewModel>(primary, new CategoryErrorViewModel());
    }

    private static AeroRequestResponse<CategoryViewModel> NotFound(string msg)
        => new(new CategoryViewModel(), new CategoryErrorViewModel { Message = msg });

    private static AeroRequestResponse<CategoryViewModel> Fail(string msg)
        => new(new CategoryViewModel(), new CategoryErrorViewModel { Message = msg });

    private static async Task<IReadOnlyDictionary<long, CategoryTranslation>> LoadTranslationsAsync(
        IDocumentSession session,
        IEnumerable<long> categoryIds,
        string culture,
        CancellationToken ct)
    {
        var ids = categoryIds.Distinct().ToArray();
        if (ids.Length == 0 || string.Equals(culture, SitesModel.DefaultCultureName, StringComparison.OrdinalIgnoreCase))
            return new Dictionary<long, CategoryTranslation>();

        var translations = await session.Query<CategoryTranslation>()
            .Where(x => x.Culture == culture && ids.Contains(x.CategoryId))
            .ToListAsync(ct);

        return translations
            .GroupBy(x => x.CategoryId)
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
