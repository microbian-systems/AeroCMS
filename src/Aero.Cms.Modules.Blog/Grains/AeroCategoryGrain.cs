using Aero.Actors;
using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Events;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Abstractions.Requests;
using Aero.Core;
using Marten;
using Microsoft.Extensions.Logging;
using Wolverine;
using IRequest = Aero.Core.Commands.IRequest;

namespace Aero.Cms.Modules.Blog.Grains;

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
        ILogger<AeroActor> log,
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

        return category is not null
            ? Ok(MapToViewModel(category))
            : NotFound($"Category {id} not found");
    }

    public async Task<AeroRequestResponse<CategoryViewModel>> GetByIdsAsync(long[] ids, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();
        var categories = await session.Query<Models.Category>()
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(ct);

        var results = categories.Select(MapToViewModel).ToList();
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

        await _bus.PublishAsync(new CategoryViewModelCreated(MapToViewModel(category)));

        return Ok(MapToViewModel(category));
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

        await _bus.PublishAsync(new CategoryViewModelUpdated(MapToViewModel(category)));

        return Ok(MapToViewModel(category));
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

        await _bus.PublishAsync(new CategoryViewModelDeleted(MapToViewModel(category)));

        return Ok(MapToViewModel(category));
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

        var results = categories.Select(MapToViewModel).ToList();
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
        var categories = await session.Query<Models.Category>()
            .Where(x => x.SiteId == siteId && x.Slug == slug)
            .ToListAsync(ct);

        var results = categories.Select(MapToViewModel).ToList();
        return Ok(results);
    }

    // ── IAeroCategoryActor.GetAllAsync ─────────────────────────────────

    public async Task<List<CategoryViewModel>> GetAllAsync(CancellationToken ct = default)
    {
        await using var session = _store.LightweightSession();
        var categories = await session.Query<Models.Category>()
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

        return categories.Select(MapToViewModel).ToList();
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

    private static CategoryViewModel MapToViewModel(Models.Category cat) => new()
    {
        Id = cat.Id,
        SiteId = cat.SiteId,
        Name = cat.Name,
        Slug = cat.Slug,
        Description = cat.Description,
        ParentCategoryId = cat.ParentCategoryId,
        CreatedOn = cat.CreatedOn,
        ModifiedOn = cat.ModifiedOn,
        CreatedBy = cat.CreatedBy,
        ModifiedBy = cat.ModifiedBy
    };
}
