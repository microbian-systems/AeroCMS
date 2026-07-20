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
/// session per persistence operation. Successful create, update, and delete
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
public async Task<AeroRequestResponse<AliasViewModel>> GetByIdAsync(long id, CancellationToken ct)
    {
        await using var session = await _store.LightweightSessionAsync();
        var doc = await session.LoadAsync<AliasDocument>(id, ct);

        return doc is not null
            ? Ok(MapToViewModel(doc))
            : NotFound($"Alias {id} not found");
    }

    /// <summary>
    /// Loads aliases whose IDs occur in <paramref name="ids"/>. Due to the
    /// response contract, only the first mapped alias is exposed as the primary
    /// response value; an empty match produces an empty view model without error.
    /// </summary>
public async Task<AeroRequestResponse<AliasViewModel>> GetByIdsAsync(long[] ids, CancellationToken ct)
    {
        await using var session = await _store.LightweightSessionAsync();
        var docs = await session.Query<AliasDocument>()
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(ct);

        var results = docs.Select(MapToViewModel).ToList();
        return Ok(results);
    }

    /// <summary>
    /// Creates a manually owned alias only when <paramref name="request"/> is a
    /// <see cref="CreateAliasRequest"/>. It normalizes culture and paths, commits
    /// the new document, then publishes <see cref="AliasCreated"/>; another
    /// request type returns an error response without persistence.
    /// </summary>
public async Task<AeroRequestResponse<AliasViewModel>> CreateAsync(IRequest request, CancellationToken ct)
    {
        if (request is not CreateAliasRequest create)
            return Fail("Expected CreateAliasRequest");

        await using var session = await _store.LightweightSessionAsync();

        var doc = new AliasDocument
        {
            Id = Snowflake.NewId(),
            SiteId = create.SiteId,
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
    /// Updates a persisted alias only when <paramref name="request"/> is an
    /// <see cref="UpdateAliasRequest"/>. It returns an error response for a
    /// mismatched request or missing document, otherwise normalizes paths and
    /// culture, commits, then publishes <see cref="AliasUpdated"/>.
    /// </summary>
public async Task<AeroRequestResponse<AliasViewModel>> UpdateAsync(IRequest request, CancellationToken ct)
    {
        if (request is not UpdateAliasRequest update)
            return Fail("Expected UpdateAliasRequest");

        await using var session = await _store.LightweightSessionAsync();
        var doc = await session.LoadAsync<AliasDocument>(update.Id, ct);

        if (doc is null)
            return NotFound($"Alias {update.Id} not found");

        doc.Culture = AliasDocument.NormalizeCulture(update.Culture);
        doc.OldPath = AliasDocument.NormalizePath(update.OldPath);
        doc.NormalizedOldPath = AliasDocument.NormalizePath(update.OldPath);
        doc.NewPath = AliasDocument.NormalizePath(update.NewPath);
        doc.Notes = update.Notes;
        doc.ModifiedOn = DateTimeOffset.UtcNow;

        session.Store(doc);
        await session.SaveChangesAsync(ct);

        await _bus.PublishAsync(new AliasUpdated(doc));

        return Ok(MapToViewModel(doc));
    }

    /// <summary>
    /// Deletes an alias only when <paramref name="request"/> is a
    /// <see cref="DeleteAliasRequest"/>. It returns an error response for a
    /// mismatched request or missing document; successful deletion commits before
    /// publishing <see cref="AliasDeleted"/>.
    /// </summary>
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
    /// Gets all aliases, optionally restricted to a site, ordered by old path.
    /// Unlike the inherited collection response methods, this method returns the
    /// complete mapped collection.
    /// </summary>
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
        Culture = doc.Culture,
        StatusCode = doc.StatusCode,
        IsAutomatic = doc.IsAutomatic,
        CreatedOn = doc.CreatedOn,
        ModifiedOn = doc.ModifiedOn,
        CreatedBy = doc.CreatedBy,
        ModifiedBy = doc.ModifiedBy
    };
}
