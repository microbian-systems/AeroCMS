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
/// Implements category actor operations over short-lived Sable sessions.
/// </summary>
/// <remarks>
/// The in-memory state methods are activation-local and independent of persisted category documents.
/// Mutations commit before publishing their Wolverine event, so persistence and notification are not
/// atomic; a publish failure can be observed after the database change has succeeded.
/// </remarks>
public sealed class AeroCategoryGrain : AeroActor, IAeroCategoryActor
{
    private readonly IDocumentStore _store;
    private readonly IMessageBus _bus;
    private CategoryViewModel _state = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="AeroCategoryGrain"/> class.
    /// </summary>
    /// <param name="log">The actor logger.</param>
    /// <param name="store">The store used to open a session for each operation.</param>
    /// <param name="bus">The bus that receives post-commit category events.</param>
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

    /// <summary>
    /// Returns the current activation-local state without reading persistence.
    /// </summary>
    /// <param name="ct">A cancellation token that is not observed because the operation is synchronous.</param>
    /// <returns>The current state reference.</returns>
public Task<CategoryViewModel> GetStateAsync(CancellationToken ct)
        => Task.FromResult(_state);

    /// <summary>
    /// Replaces the activation-local state without persisting or publishing it.
    /// </summary>
    /// <param name="state">The state reference to retain.</param>
    /// <param name="ct">A cancellation token that is not observed because the operation is synchronous.</param>
    /// <returns>A completed task.</returns>
public Task UpdateStateAsync(CategoryViewModel state, CancellationToken ct)
    {
        _state = state;
        return Task.CompletedTask;
    }

    // ── ICruddable<CategoryViewModel, long> ────────────────────────────

    /// <summary>
    /// Loads a category by identifier and overlays the current UI-culture translation when available.
    /// </summary>
    /// <param name="id">The persisted category identifier.</param>
    /// <param name="ct">A token used to cancel persistence queries.</param>
    /// <returns>The mapped category, or an error response when no document exists.</returns>
public async Task<AeroRequestResponse<CategoryViewModel>> GetByIdAsync(long id, CancellationToken ct)
    {
        await using var session = await _store.LightweightSessionAsync();
        var category = await session.LoadAsync<Models.Category>(id, ct);
        var translations = category is null
            ? new Dictionary<long, CategoryTranslation>()
            : await LoadTranslationsAsync(session, [category.Id], GetCurrentCulture(), ct);

        return category is not null
            ? Ok(PostTaxonomyTranslationMapper.MapCategory(category, translations.GetValueOrDefault(category.Id)))
            : NotFound($"Category {id} not found");
    }

    /// <summary>
    /// Loads categories whose identifiers are in the supplied array.
    /// </summary>
    /// <param name="ids">The category identifiers to query.</param>
    /// <param name="ct">A token used to cancel persistence queries.</param>
    /// <returns>
    /// A response whose data is the first mapped match, or an empty view model when none match.
    /// Additional matches are not represented by <see cref="AeroRequestResponse{T}"/>.
    /// </returns>
public async Task<AeroRequestResponse<CategoryViewModel>> GetByIdsAsync(long[] ids, CancellationToken ct)
    {
        await using var session = await _store.LightweightSessionAsync();
        var categories = await session.Query<Models.Category>()
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(ct);

        var translations = await LoadTranslationsAsync(session, categories.Select(x => x.Id), GetCurrentCulture(), ct);
        var results = categories.Select(category => PostTaxonomyTranslationMapper.MapCategory(category, translations.GetValueOrDefault(category.Id))).ToList();
        return Ok(results);
    }

    /// <summary>
    /// Creates and commits a category for a recognized create request, then publishes a created event.
    /// </summary>
    /// <param name="request">A <see cref="CreateCategoryRequest"/>; other request types produce a failure response.</param>
    /// <param name="ct">A token used for the database commit.</param>
    /// <returns>The created category or a request-type failure response.</returns>
    /// <remarks>The generated identifier makes repeated calls create distinct documents.</remarks>
public async Task<AeroRequestResponse<CategoryViewModel>> CreateAsync(IRequest request, CancellationToken ct)
    {
        if (request is not CreateCategoryRequest create)
            return Fail("Expected CreateCategoryRequest");

        await using var session = await _store.LightweightSessionAsync();

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

    /// <summary>
    /// Replaces the mutable fields of an existing category and publishes an updated event after commit.
    /// </summary>
    /// <param name="request">An <see cref="UpdateCategoryRequest"/>; other request types produce a failure response.</param>
    /// <param name="ct">A token used for persistence.</param>
    /// <returns>The updated category, a not-found response, or a request-type failure response.</returns>
public async Task<AeroRequestResponse<CategoryViewModel>> UpdateAsync(IRequest request, CancellationToken ct)
    {
        if (request is not UpdateCategoryRequest update)
            return Fail("Expected UpdateCategoryRequest");

        await using var session = await _store.LightweightSessionAsync();
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

    /// <summary>
    /// Deletes an existing category and publishes a deleted event after commit.
    /// </summary>
    /// <param name="request">A <see cref="DeleteCategoryRequest"/>; other request types produce a failure response.</param>
    /// <param name="ct">A token used for persistence.</param>
    /// <returns>The deleted category, a not-found response, or a request-type failure response.</returns>
    /// <remarks>This actor does not check whether posts still reference the category.</remarks>
public async Task<AeroRequestResponse<CategoryViewModel>> DeleteAsync(IRequest request, CancellationToken ct)
    {
        if (request is not DeleteCategoryRequest delete)
            return Fail("Expected DeleteCategoryRequest");

        await using var session = await _store.LightweightSessionAsync();
        var category = await session.LoadAsync<Models.Category>(delete.Id, ct);

        if (category is null)
            return NotFound($"Category {delete.Id} not found");

        session.Delete(category);
        await session.SaveChangesAsync(ct);

        await _bus.PublishAsync(new CategoryViewModelDeleted(PostTaxonomyTranslationMapper.MapCategory(category)));

        return Ok(PostTaxonomyTranslationMapper.MapCategory(category));
    }

    // ── ICanFindBySite<CategoryViewModel, long> ────────────────────────

    /// <summary>
    /// Returns the first category from a name-ordered page for one site.
    /// </summary>
    /// <param name="siteId">The owning site identifier.</param>
    /// <param name="page">The one-based page number used to calculate the query offset.</param>
    /// <param name="rows">The maximum number of rows queried.</param>
    /// <param name="ct">A token used to cancel persistence queries.</param>
    /// <returns>The first mapped category in the page, or an empty view model when the page is empty.</returns>
public async Task<AeroRequestResponse<CategoryViewModel>> GetBySiteIdAsync(
        long siteId,
        int page = 1,
        int rows = 10,
        CancellationToken ct = default)
    {
        await using var session = await _store.LightweightSessionAsync();
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

    /// <summary>
    /// Finds the first category for a site by its base or current-culture translated slug.
    /// </summary>
    /// <param name="siteId">The owning site identifier.</param>
    /// <param name="slug">The exact slug to match.</param>
    /// <param name="ct">A token used to cancel persistence queries.</param>
    /// <returns>The first match, or an empty successful response when no category matches.</returns>
public Task<AeroRequestResponse<CategoryViewModel>> GetBySlugAsync(long siteId, string slug, CancellationToken ct)
        => GetBySlugCoreAsync(siteId, slug, ct);

    /// <summary>
    /// Adapts the string site-key contract to the numeric site identifier used by persistence.
    /// </summary>
    /// <param name="siteId">The numeric site identifier encoded as text.</param>
    /// <param name="slug">The exact slug to match.</param>
    /// <param name="ct">A token used to cancel persistence queries.</param>
    /// <returns>A lookup response, or a failure response when the site key is not a valid <see cref="long"/>.</returns>
    Task<AeroRequestResponse<CategoryViewModel>> ICanFindBySlug<CategoryViewModel, string>.GetBySlugAsync(string siteId, string slug, CancellationToken ct)
    {
        if (long.TryParse(siteId, out var id))
            return GetBySlugCoreAsync(id, slug, ct);
        return Task.FromResult(Fail($"Invalid site ID: {siteId}"));
    }

    /// <summary>
    /// Queries base and translated slug candidates, constrains their categories to a site, and maps the current culture.
    /// </summary>
    private async Task<AeroRequestResponse<CategoryViewModel>> GetBySlugCoreAsync(long siteId, string slug, CancellationToken ct)
    {
        await using var session = await _store.LightweightSessionAsync();
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

    /// <summary>
    /// Returns every category across all sites, ordered by base name and overlaid for the current UI culture.
    /// </summary>
    /// <param name="ct">A token used to cancel persistence queries.</param>
    /// <returns>All mapped categories; callers that require tenant isolation must filter by <c>SiteId</c>.</returns>
public async Task<List<CategoryViewModel>> GetAllAsync(CancellationToken ct = default)
    {
        await using var session = await _store.LightweightSessionAsync();
        var categories = await session.Query<Models.Category>()
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

        var translations = await LoadTranslationsAsync(session, categories.Select(x => x.Id), GetCurrentCulture(), ct);
        return categories.Select(category => PostTaxonomyTranslationMapper.MapCategory(category, translations.GetValueOrDefault(category.Id))).ToList();
    }

    // ── Helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Produces the grain's simple lowercase, space-to-hyphen fallback slug.
    /// </summary>
    private static string GenerateSlug(string name) =>
        name.ToLowerInvariant().Replace(' ', '-').Replace("--", "-");

    /// <summary>
    /// Creates a successful response around one category.
    /// </summary>
    private static AeroRequestResponse<CategoryViewModel> Ok(CategoryViewModel vm)
        => new(vm, new CategoryErrorViewModel());

    /// <summary>
    /// Adapts a list to the single-data response contract by selecting its first item.
    /// </summary>
    private static AeroRequestResponse<CategoryViewModel> Ok(IReadOnlyList<CategoryViewModel> list)
    {
        var primary = list.Count > 0 ? list[0] : new CategoryViewModel();
        return new AeroRequestResponse<CategoryViewModel>(primary, new CategoryErrorViewModel());
    }

    /// <summary>
    /// Creates an error response with an empty category payload.
    /// </summary>
    private static AeroRequestResponse<CategoryViewModel> NotFound(string msg)
        => new(new CategoryViewModel(), new CategoryErrorViewModel { Message = msg });

    /// <summary>
    /// Creates a request failure response with an empty category payload.
    /// </summary>
    private static AeroRequestResponse<CategoryViewModel> Fail(string msg)
        => new(new CategoryViewModel(), new CategoryErrorViewModel { Message = msg });

    /// <summary>
    /// Loads at most one translation per category for a non-default culture.
    /// </summary>
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
