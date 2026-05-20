using Aero.Actors;
using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Events;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Abstractions.Requests;
using Aero.Cms.Core.Entities;
using Aero.Core;
using Marten;
using Microsoft.Extensions.Logging;
using Wolverine;
using IRequest = Aero.Core.Commands.IRequest;

namespace Aero.Cms.Modules.Blog.Grains;

/// <summary>
/// Orleans grain for tag management — wraps Marten persistence behind
/// <see cref="IAeroTagActor"/>. Publishes Wolverine events after mutations.
/// </summary>
public sealed class AeroTagGrain : AeroActor, IAeroTagActor
{
    private readonly IDocumentStore _store;
    private readonly IMessageBus _bus;
    private TagViewModel _state = new();

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

    public Task<TagViewModel> GetStateAsync(CancellationToken ct)
        => Task.FromResult(_state);

    public Task UpdateStateAsync(TagViewModel state, CancellationToken ct)
    {
        _state = state;
        return Task.CompletedTask;
    }

    // ── ICruddable<TagViewModel, long> ────────────────────────────────

    public async Task<AeroRequestResponse<TagViewModel>> GetByIdAsync(long id, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();
        var tag = await session.LoadAsync<Models.Tag>(id, ct);

        return tag is not null
            ? Ok(MapToViewModel(tag))
            : NotFound($"Tag {id} not found");
    }

    public async Task<AeroRequestResponse<TagViewModel>> GetByIdsAsync(long[] ids, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();
        var tags = await session.Query<Models.Tag>()
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(ct);

        var results = tags.Select(MapToViewModel).ToList();
        return Ok(results);
    }

    public async Task<AeroRequestResponse<TagViewModel>> CreateAsync(IRequest request, CancellationToken ct)
    {
        if (request is not CreateTagRequest create)
            return Fail("Expected CreateTagRequest");

        await using var session = _store.LightweightSession();

        var tag = new Models.Tag
        {
            Id = Snowflake.NewId(),
            SiteId = create.siteId,
            Name = create.Name,
            Slug = create.Slug ?? GenerateSlug(create.Name),
        };

        session.Store(tag);
        await session.SaveChangesAsync(ct);

        await _bus.PublishAsync(new TagViewModelCreated(MapToViewModel(tag)));

        return Ok(MapToViewModel(tag));
    }

    public async Task<AeroRequestResponse<TagViewModel>> UpdateAsync(IRequest request, CancellationToken ct)
    {
        if (request is not UpdateTagRequest update)
            return Fail("Expected UpdateTagRequest");

        await using var session = _store.LightweightSession();
        var tag = await session.LoadAsync<Models.Tag>(update.Id, ct);

        if (tag is null)
            return NotFound($"Tag {update.Id} not found");

        tag.Name = update.Name;
        tag.Slug = update.Slug ?? GenerateSlug(update.Name);
        tag.ModifiedOn = DateTimeOffset.UtcNow;

        session.Store(tag);
        await session.SaveChangesAsync(ct);

        await _bus.PublishAsync(new TagViewModelUpdated(MapToViewModel(tag)));

        return Ok(MapToViewModel(tag));
    }

    public async Task<AeroRequestResponse<TagViewModel>> DeleteAsync(IRequest request, CancellationToken ct)
    {
        if (request is not DeleteTagRequest delete)
            return Fail("Expected DeleteTagRequest");

        await using var session = _store.LightweightSession();
        var tag = await session.LoadAsync<Models.Tag>(delete.Id, ct);

        if (tag is null)
            return NotFound($"Tag {delete.Id} not found");

        session.Delete(tag);
        await session.SaveChangesAsync(ct);

        await _bus.PublishAsync(new TagViewModelDeleted(MapToViewModel(tag)));

        return Ok(MapToViewModel(tag));
    }

    // ── ICanFindBySite<TagViewModel, long> ────────────────────────────

    public async Task<AeroRequestResponse<TagViewModel>> GetBySiteIdAsync(
        long siteId,
        int page = 1,
        int rows = 10,
        CancellationToken ct = default)
    {
        await using var session = _store.LightweightSession();
        var tags = await session.Query<Models.Tag>()
            .Where(x => x.SiteId == siteId)
            .OrderBy(x => x.Name)
            .Skip((page - 1) * rows)
            .Take(rows)
            .ToListAsync(ct);

        var results = tags.Select(MapToViewModel).ToList();
        return Ok(results);
    }

    // ── ICanFindBySlug ────────────────────────────────────────────────

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
        await using var session = _store.LightweightSession();
        var tags = await session.Query<Models.Tag>()
            .Where(x => x.SiteId == siteId && x.Slug == slug)
            .ToListAsync(ct);

        var results = tags.Select(MapToViewModel).ToList();
        return Ok(results);
    }

    // ── IAeroTagActor.GetAllAsync ─────────────────────────────────────

    public async Task<List<TagViewModel>> GetAllAsync(CancellationToken ct = default)
    {
        await using var session = _store.LightweightSession();
        var tags = await session.Query<Models.Tag>()
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

        return tags.Select(MapToViewModel).ToList();
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

    private static TagViewModel MapToViewModel(Models.Tag tag) => new()
    {
        Id = tag.Id,
        SiteId = tag.SiteId,
        Name = tag.Name,
        Slug = tag.Slug,
        CreatedOn = tag.CreatedOn,
        ModifiedOn = tag.ModifiedOn,
        CreatedBy = tag.CreatedBy,
        ModifiedBy = tag.ModifiedBy
    };
}
