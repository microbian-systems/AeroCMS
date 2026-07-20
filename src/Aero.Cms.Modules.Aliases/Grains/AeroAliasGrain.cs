using Aero.Actors;
using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Abstractions.Requests;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Aliases.Events;
using Aero.Core;
using AeroDB.Sable;
using Microsoft.Extensions.Logging;
using Wolverine;
using IRequest = Aero.Core.Commands.IRequest;

namespace Aero.Cms.Modules.Aliases.Grains;

/// <summary>
/// Orleans actor implementing alias CRUD through a fresh lightweight document
/// session per persistence operation. Successful scoped create and delete
/// commits occur before their corresponding Wolverine event is published.
/// <para>
/// Its in-memory <see cref="AliasViewModel"/> state is separate from persisted
/// aliases and is only changed by <see cref="UpdateStateAsync"/>. Collection
/// query methods are constrained by the inherited response contract: they map
/// a collection but return only its first item as the response's canonical value.
/// Call <see cref="GetAllAliasesAsync"/> when the complete collection is needed.
/// </para>
/// <para>
/// Mutation failures are not transactional across persistence and messaging.
/// Persistence is committed before the <see cref="IMessageBus"/> publication is
/// awaited, so cancellation, transport, or publication failures can propagate
/// after the document is already committed and cannot roll it back. Persistence,
/// uniqueness, and provider exceptions also propagate rather than being encoded
/// in <see cref="AeroRequestResponse{AliasViewModel}"/>.
/// </para>
/// </summary>
public sealed class AeroAliasGrain : AeroActor, IAeroAliasActor
{
    private readonly IDocumentStore _store;
    private readonly IMessageBus _bus;
    private AliasViewModel _state = new();

    /// <summary>Initializes the actor with the shared document store and message bus.</summary>
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

    /// <summary>
    /// Returns the actor-local state without reading persistence. The cancellation
    /// token is accepted for the interface contract but is not observed.
    /// </summary>
public Task<AliasViewModel> GetStateAsync(CancellationToken ct)
        => Task.FromResult(_state);

    /// <summary>
    /// Replaces actor-local state without writing an <see cref="AliasDocument"/>
    /// or publishing an event. The cancellation token is not observed.
    /// </summary>
public Task UpdateStateAsync(AliasViewModel state, CancellationToken ct)
    {
        _state = state;
        return Task.CompletedTask;
    }

    // ── ICruddable<AliasViewModel, long> ──────────────────────────────

    /// <summary>Loads an alias by ID, returning a response whose error message reports absence.</summary>
public Task<AeroRequestResponse<AliasViewModel>> GetByIdAsync(long id, CancellationToken ct)
        => Task.FromResult(Fail("A site scope is required to load an alias by identifier"));

public async Task<AeroRequestResponse<AliasViewModel>> GetByIdAsync(long id, long siteId, CancellationToken ct)
{
    await using var session = await _store.LightweightSessionAsync();
    var doc = await session.Query<AliasDocument>()
        .FirstOrDefaultAsync(x => x.Id == id && x.SiteId == siteId, ct);
    return doc is not null ? Ok(MapToViewModel(doc)) : NotFound($"Alias {id} not found");
}

    /// <summary>
    /// Rejects the inherited identifier-only collection lookup because it has no site boundary.
    /// </summary>
public Task<AeroRequestResponse<AliasViewModel>> GetByIdsAsync(long[] ids, CancellationToken ct)
        => Task.FromResult(Fail("A site scope is required to load aliases by identifier"));

    /// <summary>
    /// Rejects the inherited create contract because it has no trusted site argument.
    /// </summary>
public Task<AeroRequestResponse<AliasViewModel>> CreateAsync(IRequest request, CancellationToken ct)
        => Task.FromResult(Fail("A site-scoped alias create operation is required"));

public async Task<AeroRequestResponse<AliasViewModel>> CreateAliasAsync(CreateAliasRequest create, long siteId, CancellationToken ct)
{
    await using var session = await _store.LightweightSessionAsync();
    var doc = new AliasDocument
    {
        Id = Snowflake.NewId(),
        SiteId = siteId,
        Culture = AliasDocument.NormalizeCulture(create.Culture),
        OldPath = AliasDocument.NormalizePath(create.OldPath),
        NormalizedOldPath = AliasDocument.NormalizePath(create.OldPath),
        NewPath = AliasDocument.NormalizePath(create.NewPath),
        Notes = create.Notes,
        OwnerId = null,
        OwnerType = null,
        IsAutomatic = false
    };
    session.Store(doc);
    await session.SaveChangesAsync(ct);
    await _bus.PublishAsync(new AliasCreated(doc));
    return Ok(MapToViewModel(doc));
}

    /// <summary>
    /// Rejects inherited updates; the administrative alias API has no update route.
    /// </summary>
public Task<AeroRequestResponse<AliasViewModel>> UpdateAsync(IRequest request, CancellationToken ct)
        => Task.FromResult(Fail("Alias updates are not supported by the administrative API"));

    /// <summary>
    /// Rejects the inherited delete contract because it has no trusted site argument.
    /// </summary>
public Task<AeroRequestResponse<AliasViewModel>> DeleteAsync(IRequest request, CancellationToken ct)
        => Task.FromResult(Fail("A site-scoped alias delete operation is required"));

public async Task<AeroRequestResponse<AliasViewModel>> DeleteAliasAsync(long id, long siteId, CancellationToken ct)
{
    await using var session = await _store.LightweightSessionAsync();
    var doc = await session.Query<AliasDocument>()
        .FirstOrDefaultAsync(x => x.Id == id && x.SiteId == siteId, ct);
    if (doc is null) return NotFound($"Alias {id} not found");
    session.Delete(doc);
    await session.SaveChangesAsync(ct);
    await _bus.PublishAsync(new AliasDeleted(doc));
    return Ok(MapToViewModel(doc));
}

    // ── ICanFindBySite<AliasViewModel, long> ──────────────────────────

    /// <summary>
    /// Gets a page of aliases for a site ordered by old path. Page and row values
    /// are used directly to calculate the query offset and are not validated.
    /// As with other collection response methods, only the first result is
    /// returned as the response's primary value.
    /// </summary>
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

    /// <summary>
    /// Finds aliases for a site whose old or new path exactly equals
    /// <paramref name="slug"/>. This actor query does not normalize the input;
    /// only the first match is available as the response's primary value.
    /// </summary>
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

    /// <summary>
    /// Gets all aliases for the supplied site, ordered by old path.
    /// Unlike the inherited collection response methods, this method returns the
    /// complete mapped collection.
    /// </summary>
public async Task<List<AliasViewModel>> GetAllAliasesAsync(
        long siteId,
        CancellationToken ct = default)
    {
        await using var session = await _store.LightweightSessionAsync();

        var docs = await session.Query<AliasDocument>()
            .Where(x => x.SiteId == siteId)
            .OrderBy(x => x.OldPath).ToListAsync(ct);
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
        Culture = doc.Culture,
        StatusCode = doc.StatusCode,
        IsAutomatic = doc.IsAutomatic,
        CreatedOn = doc.CreatedOn,
        ModifiedOn = doc.ModifiedOn,
        CreatedBy = doc.CreatedBy,
        ModifiedBy = doc.ModifiedBy
    };
}
