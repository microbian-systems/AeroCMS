using Aero.Actors;
using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Abstractions.Requests;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Aliases.Events;
using Aero.Core;
using AeroDB;
using Microsoft.Extensions.Logging;
using Wolverine;
using IRequest = Aero.Core.Commands.IRequest;

namespace Aero.Cms.Modules.Aliases.Grains;

/// <summary>
/// Orleans grain for alias management — wraps AeroDB persistence behind
/// the <see cref="IAeroAliasActor"/> interface.
///
/// Follows the AeroDB integration pattern: <see cref="IDocumentStore"/> as a
/// singleton, lightweight session per operation. No <see cref="IDocumentSession"/>
/// stored as grain state.
///
/// Publishes Wolverine events (<see cref="AliasCreated"/>, <see cref="AliasUpdated"/>,
/// <see cref="AliasDeleted"/>) after each mutation for cache invalidation and
/// downstream workflows.
/// </summary>
public sealed class AeroAliasGrain : AeroActor, IAeroAliasActor
{
    private readonly IDocumentStore _store;
    private readonly IMessageBus _bus;
    private AliasViewModel _state = new();

    public AeroAliasGrain(
        ILogger<AeroActor> log,
        IDocumentStore store,
        IMessageBus bus)
        : base(log)
    {
        _store = store;
        _bus = bus;
    }

    // ── IHaveState<AliasViewModel> ────────────────────────────────────

    public Task<AliasViewModel> GetStateAsync(CancellationToken ct)
        => Task.FromResult(_state);

    public Task UpdateStateAsync(AliasViewModel state, CancellationToken ct)
    {
        _state = state;
        return Task.CompletedTask;
    }

    // ── ICruddable<AliasViewModel, long> ──────────────────────────────

    public async Task<AeroRequestResponse<AliasViewModel>> GetByIdAsync(long id, CancellationToken ct)
    {
        await using var session = await _store.LightweightSessionAsync();
        var doc = await session.LoadAsync<AliasDocument>(id, ct);

        return doc is not null
            ? Ok(MapToViewModel(doc))
            : NotFound($"Alias {id} not found");
    }

    public async Task<AeroRequestResponse<AliasViewModel>> GetByIdsAsync(long[] ids, CancellationToken ct)
    {
        await using var session = await _store.LightweightSessionAsync();
        var docs = await session.Query<AliasDocument>()
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(ct);

        var results = docs.Select(MapToViewModel).ToList();
        return Ok(results);
    }

    public async Task<AeroRequestResponse<AliasViewModel>> CreateAsync(IRequest request, CancellationToken ct)
    {
        if (request is not CreateAliasRequest create)
            return Fail("Expected CreateAliasRequest");

        await using var session = await _store.LightweightSessionAsync();

        var doc = new AliasDocument
        {
            Id = Snowflake.NewId(),
            SiteId = create.SiteId,
            OldPath = create.OldPath,
            NewPath = create.NewPath,
            Notes = create.Notes
        };

        session.Store(doc);
        await session.SaveChangesAsync(ct);

        await _bus.PublishAsync(new AliasCreated(doc));

        return Ok(MapToViewModel(doc));
    }

    public async Task<AeroRequestResponse<AliasViewModel>> UpdateAsync(IRequest request, CancellationToken ct)
    {
        if (request is not UpdateAliasRequest update)
            return Fail("Expected UpdateAliasRequest");

        await using var session = await _store.LightweightSessionAsync();
        var doc = await session.LoadAsync<AliasDocument>(update.Id, ct);

        if (doc is null)
            return NotFound($"Alias {update.Id} not found");

        doc.OldPath = update.OldPath;
        doc.NewPath = update.NewPath;
        doc.Notes = update.Notes;
        doc.ModifiedOn = DateTimeOffset.UtcNow;

        session.Store(doc);
        await session.SaveChangesAsync(ct);

        await _bus.PublishAsync(new AliasUpdated(doc));

        return Ok(MapToViewModel(doc));
    }

    public async Task<AeroRequestResponse<AliasViewModel>> DeleteAsync(IRequest request, CancellationToken ct)
    {
        if (request is not DeleteAliasRequest delete)
            return Fail("Expected DeleteAliasRequest");

        await using var session = await _store.LightweightSessionAsync();
        var doc = await session.LoadAsync<AliasDocument>(delete.Id, ct);

        if (doc is null)
            return NotFound($"Alias {delete.Id} not found");

        session.Delete(doc);
        await session.SaveChangesAsync(ct);

        await _bus.PublishAsync(new AliasDeleted(doc));

        return Ok(MapToViewModel(doc));
    }

    // ── ICanFindBySite<AliasViewModel, long> ──────────────────────────

    public async Task<AeroRequestResponse<AliasViewModel>> GetBySiteIdAsync(
        long siteId,
        int page = 1,
        int rows = 10,
        CancellationToken ct = default)
    {
        await using var session = await _store.LightweightSessionAsync();

        var docs = await session.Query<AliasDocument>()
            .Where(x => x.SiteId == siteId)
            .OrderBy(x => x.OldPath)
            .Skip((page - 1) * rows)
            .Take(rows)
            .ToListAsync(ct);

        var results = docs.Select(MapToViewModel).ToList();
        return Ok(results);
    }

    // ── ICanFindBySlug (long key + string key overloads) ──────────────

    public Task<AeroRequestResponse<AliasViewModel>> GetBySlugAsync(long siteId, string slug, CancellationToken ct)
        => GetBySlugCoreAsync(siteId, slug, ct);

    Task<AeroRequestResponse<AliasViewModel>> ICanFindBySlug<AliasViewModel, string>.GetBySlugAsync(string siteId, string slug, CancellationToken ct)
    {
        if (long.TryParse(siteId, out var id))
            return GetBySlugCoreAsync(id, slug, ct);

        return Task.FromResult(Fail($"Invalid site ID: {siteId}"));
    }

    private async Task<AeroRequestResponse<AliasViewModel>> GetBySlugCoreAsync(long siteId, string slug, CancellationToken ct)
    {
        await using var session = await _store.LightweightSessionAsync();

        var docs = await session.Query<AliasDocument>()
            .Where(x => x.SiteId == siteId && (x.OldPath == slug || x.NewPath == slug))
            .ToListAsync(ct);

        var results = docs.Select(MapToViewModel).ToList();
        return Ok(results);
    }

    // ── IAeroAliasActor.GetAllAliasesAsync ────────────────────────────

    public async Task<List<AliasViewModel>> GetAllAliasesAsync(
        long? siteId = null,
        CancellationToken ct = default)
    {
        await using var session = await _store.LightweightSessionAsync();

        IQueryable<AliasDocument> query = session.Query<AliasDocument>();

        if (siteId.HasValue)
            query = query.Where(x => x.SiteId == siteId.Value);

        var docs = await query.OrderBy(x => x.OldPath).ToListAsync(ct);
        return docs.Select(MapToViewModel).ToList();
    }

    // ── AeroRequestResponse helpers ────────────────────────────────────

    private static AeroRequestResponse<AliasViewModel> Ok(AliasViewModel vm)
        => new(vm, new AliasErrorViewModel());

    private static AeroRequestResponse<AliasViewModel> Ok(IReadOnlyList<AliasViewModel> list)
    {
        // Interface returns single AliasViewModel; return first item as canonical result.
        // TODO: Extend IAeroCmsContentActor<T> with collection-returning overloads.
        var primary = list.Count > 0 ? list[0] : new AliasViewModel();
        return new AeroRequestResponse<AliasViewModel>(primary, new AliasErrorViewModel());
    }

    private static AeroRequestResponse<AliasViewModel> NotFound(string msg)
        => new(new AliasViewModel(), new AliasErrorViewModel { Message = msg });

    private static AeroRequestResponse<AliasViewModel> Fail(string msg)
        => new(new AliasViewModel(), new AliasErrorViewModel { Message = msg });

    // ── Mapping ───────────────────────────────────────────────────────

    private static AliasViewModel MapToViewModel(AliasDocument doc) => new()
    {
        Id = doc.Id,
        SiteId = doc.SiteId,
        OldPath = doc.OldPath,
        NewPath = doc.NewPath,
        Notes = doc.Notes,
        CreatedOn = doc.CreatedOn,
        ModifiedOn = doc.ModifiedOn,
        CreatedBy = doc.CreatedBy,
        ModifiedBy = doc.ModifiedBy
    };
}
