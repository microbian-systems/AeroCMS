using Aero.Actors;
using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Events;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Core.Models;
using Wolverine;
using IRequest = Aero.Core.Commands.IRequest;

namespace Aero.Cms.Modules.Media.Grains;

/// <summary>
/// Orleans grain for media asset management — wraps AeroDB persistence.
/// Publishes Wolverine events after mutations.
/// File I/O (disk writes) remains in the API layer — grain handles persistence only.
/// </summary>
/// <remarks>
/// Identifier-based operations do not independently enforce authorization or site ownership.
/// Callers must verify those boundaries before invoking the actor. Mutation events are published
/// after database commits and are not transactionally coordinated with persistence.
/// </remarks>
public sealed class AeroMediaGrain : AeroActor, IAeroMediaActor
{
    private readonly IDocumentStore _store;
    private readonly IMessageBus _bus;
    private MediaViewModel _state = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="AeroMediaGrain"/> class.
    /// </summary>
    /// <param name="log">The base actor logger.</param>
    /// <param name="store">Creates lightweight sessions for each operation.</param>
    /// <param name="bus">Publishes media lifecycle events after commits.</param>
public AeroMediaGrain(
        ILogger<AeroActor> log,
        IDocumentStore store,
        IMessageBus bus)
        : base(log)
    {
        _store = store;
        _bus = bus;
    }

    // ── IHaveState<MediaViewModel> ────────────────────────────────────

    /// <summary>
    /// Returns the grain's in-memory state snapshot.
    /// </summary>
    /// <param name="ct">Unused; no asynchronous work is performed.</param>
    /// <returns>The currently stored view-model reference.</returns>
public Task<MediaViewModel> GetStateAsync(CancellationToken ct)
        => Task.FromResult(_state);

    /// <summary>
    /// Replaces the grain's in-memory state snapshot without persisting it.
    /// </summary>
    /// <param name="state">The new state reference.</param>
    /// <param name="ct">Unused; no asynchronous work is performed.</param>
    /// <returns>A completed task.</returns>
public Task UpdateStateAsync(MediaViewModel state, CancellationToken ct)
    {
        _state = state;
        return Task.CompletedTask;
    }

    // ── ICruddable<MediaViewModel, long> ──────────────────────────────

    /// <summary>
    /// Loads a media document by identifier and maps it to the actor response model.
    /// </summary>
    /// <param name="id">The document identifier.</param>
    /// <param name="ct">Cancels session creation or the lookup.</param>
    /// <returns>A successful mapped response, or a not-found response.</returns>
public async Task<AeroRequestResponse<MediaViewModel>> GetByIdAsync(long id, CancellationToken ct)
    {
        await using var session = await _store.LightweightSessionAsync();
        var asset = await session.LoadAsync<MediaAsset>(id, ct);
        return asset is not null
            ? Ok(MapToViewModel(asset))
            : NotFound($"Media {id} not found");
    }

    /// <summary>
    /// Loads the documents whose identifiers are contained in the supplied array.
    /// </summary>
    /// <param name="ids">The identifiers to query.</param>
    /// <param name="ct">Cancels the query.</param>
    /// <returns>
    /// A response whose data is only the first matching asset, or a default model when none match;
    /// the current response shape does not carry the complete list.
    /// </returns>
public async Task<AeroRequestResponse<MediaViewModel>> GetByIdsAsync(long[] ids, CancellationToken ct)
    {
        await using var session = await _store.LightweightSessionAsync();
        var assets = await session.Query<MediaAsset>()
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(ct);
        var results = assets.Select(MapToViewModel).ToList();
        return Ok(results);
    }

    // ICruddable stubs — use SaveMediaAsync / DeleteMediaAsync instead
    /// <summary>
    /// Rejects the generic create contract in favor of <see cref="SaveMediaAsync"/>.
    /// </summary>
    /// <returns>A failed response directing the caller to <see cref="SaveMediaAsync"/>.</returns>
public Task<AeroRequestResponse<MediaViewModel>> CreateAsync(IRequest request, CancellationToken ct)
        => Task.FromResult(Fail("Use SaveMediaAsync"));
    /// <summary>
    /// Rejects the generic update contract in favor of <see cref="SaveMediaAsync"/>.
    /// </summary>
    /// <returns>A failed response directing the caller to <see cref="SaveMediaAsync"/>.</returns>
public Task<AeroRequestResponse<MediaViewModel>> UpdateAsync(IRequest request, CancellationToken ct)
        => Task.FromResult(Fail("Use SaveMediaAsync"));
    /// <summary>
    /// Rejects the generic delete contract in favor of <see cref="DeleteMediaAsync"/>.
    /// </summary>
    /// <returns>A failed response directing the caller to <see cref="DeleteMediaAsync"/>.</returns>
public Task<AeroRequestResponse<MediaViewModel>> DeleteAsync(IRequest request, CancellationToken ct)
        => Task.FromResult(Fail("Use DeleteMediaAsync"));

    // ── ICanFindBySite ────────────────────────────────────────────────

    /// <summary>
    /// Loads one page of media documents belonging to a site.
    /// </summary>
    /// <param name="siteId">The site identifier to filter.</param>
    /// <param name="page">The one-based page number; values below one are not rejected.</param>
    /// <param name="rows">The requested page size; the method does not impose bounds.</param>
    /// <param name="ct">Cancels the query.</param>
    /// <returns>
    /// A response whose data is only the first matching asset, or a default model when none match;
    /// the complete materialized page is not represented by the current response shape.
    /// </returns>
public async Task<AeroRequestResponse<MediaViewModel>> GetBySiteIdAsync(
        long siteId, int page = 1, int rows = 10, CancellationToken ct = default)
    {
        await using var session = await _store.LightweightSessionAsync();
        var assets = await session.Query<MediaAsset>()
            .Where(x => x.SiteId == siteId)
            .OrderBy(x => x.FileName)
            .Skip((page - 1) * rows).Take(rows)
            .ToListAsync(ct);
        var results = assets.Select(MapToViewModel).ToList();
        return Ok(results);
    }

    // ── ICanFindBySlug ────────────────────────────────────────────────

    /// <summary>
    /// Finds media whose URL contains a slug within the supplied site.
    /// </summary>
    /// <param name="siteId">The site identifier to filter.</param>
    /// <param name="slug">The provider-translated URL substring.</param>
    /// <param name="ct">Cancels the query.</param>
    /// <returns>A response containing only the first match, or a default model when none match.</returns>
public Task<AeroRequestResponse<MediaViewModel>> GetBySlugAsync(long siteId, string slug, CancellationToken ct)
        => GetBySlugCoreAsync(siteId, slug, ct);

    /// <summary>
    /// Adapts the string-site interface contract to the numeric site identifier used by media documents.
    /// </summary>
    /// <returns>A slug lookup response, or a failure when <paramref name="siteId"/> is not a valid <see cref="long"/>.</returns>
    Task<AeroRequestResponse<MediaViewModel>> ICanFindBySlug<MediaViewModel, string>.GetBySlugAsync(string siteId, string slug, CancellationToken ct)
    {
        if (long.TryParse(siteId, out var id))
            return GetBySlugCoreAsync(id, slug, ct);
        return Task.FromResult(Fail($"Invalid site ID: {siteId}"));
    }

    /// <summary>
    /// Executes the site-and-URL-substring query used by both slug contract forms.
    /// </summary>
    /// <returns>A response containing only the first match, or a default model when none match.</returns>
    private async Task<AeroRequestResponse<MediaViewModel>> GetBySlugCoreAsync(long siteId, string slug, CancellationToken ct)
    {
        await using var session = await _store.LightweightSessionAsync();
        var assets = await session.Query<MediaAsset>()
            .Where(x => x.SiteId == siteId && x.Url != null && x.Url.Contains(slug))
            .ToListAsync(ct);
        var results = assets.Select(MapToViewModel).ToList();
        return Ok(results);
    }

    // ── IAeroMediaActor custom methods ────────────────────────────────

    /// <summary>
    /// Loads all media documents without a site filter.
    /// </summary>
    /// <param name="ct">Cancels the query.</param>
    /// <returns>All mapped media models ordered by file name.</returns>
public async Task<List<MediaViewModel>> GetAllAsync(CancellationToken ct = default)
    {
        await using var session = await _store.LightweightSessionAsync();
        var assets = await session.Query<MediaAsset>()
            .OrderBy(x => x.FileName)
            .ToListAsync(ct);
        return assets.Select(MapToViewModel).ToList();
    }

    /// <summary>
    /// Loads a page of media documents without a site filter.
    /// </summary>
    /// <param name="parentId">The optional parent filter; root-only filtering is not applied when absent.</param>
    /// <param name="skip">The number of matching documents to skip.</param>
    /// <param name="take">The maximum number of documents to return.</param>
    /// <param name="search">An optional case-sensitive file-name substring.</param>
    /// <param name="ct">Cancels the count or page query.</param>
    /// <returns>The mapped page and the total number of matching documents.</returns>
public async Task<(List<MediaViewModel> Items, long TotalCount)> GetPagedAsync(
        long? parentId, int skip, int take, string? search, CancellationToken ct = default)
    {
        await using var session = await _store.LightweightSessionAsync();
        IQueryable<MediaAsset> query = session.Query<MediaAsset>();

        if (!string.IsNullOrEmpty(search))
            query = query.Where(x => x.FileName.Contains(search));
        if (parentId.HasValue)
            query = query.Where(x => x.ParentId == parentId.Value);

        var totalCount = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.FileName)
            .Skip(skip).Take(take)
            .ToListAsync(ct);

        return (items.Select(MapToViewModel).ToList(), totalCount);
    }

    /// <summary>
    /// Creates a media document when its identifier is absent, or updates selected metadata when present.
    /// </summary>
    /// <param name="vm">The caller-supplied media state.</param>
    /// <param name="ct">Cancels database work.</param>
    /// <returns>The persisted media state in an actor response.</returns>
    /// <remarks>
    /// New records receive a Snowflake identifier and trust the supplied <c>SiteId</c>. Updates load
    /// by identifier without a site check and change only file name, alternate text, description,
    /// and modification time. The database commit precedes event publication; a bus failure can
    /// therefore escape after persistence has succeeded.
    /// </remarks>
public async Task<AeroRequestResponse<MediaViewModel>> SaveMediaAsync(MediaViewModel vm, CancellationToken ct = default)
    {
        await using var session = await _store.LightweightSessionAsync();
        var existing = await session.LoadAsync<MediaAsset>(vm.Id, ct);
        bool isNew = existing is null;

        if (isNew)
        {
            var asset = new MediaAsset
            {
                Id = Snowflake.NewId(),
                FileName = vm.FileName ?? vm.Title ?? "",
                MimeType = vm.MimeType?.ToString() ?? "",
                FileSize = vm.FileSizeInBytes,
                Url = vm.Url ?? "",
                AltText = vm.AltText,
                Description = vm.Description,
                IsFolder = vm.IsFolder,
                ParentId = vm.ParentId,
                Width = vm.Dimensions.Width,
                Height = vm.Dimensions.Height,
                SiteId = vm.SiteId
            };
            session.Store(asset);
            await session.SaveChangesAsync(ct);
            await _bus.PublishAsync(new MediaViewModelCreated(MapToViewModel(asset)));
            return Ok(MapToViewModel(asset));
        }
        else
        {
            existing.FileName = vm.FileName ?? vm.Title ?? "";
            existing.AltText = vm.AltText;
            existing.Description = vm.Description;
            existing.ModifiedOn = DateTimeOffset.UtcNow;
            session.Store(existing);
            await session.SaveChangesAsync(ct);
            await _bus.PublishAsync(new MediaViewModelUpdated(MapToViewModel(existing)));
            return Ok(MapToViewModel(existing));
        }
    }

    /// <summary>
    /// Deletes a media document by identifier and publishes its deletion event.
    /// </summary>
    /// <param name="id">The document identifier.</param>
    /// <param name="ct">Cancels database work.</param>
    /// <returns>The deleted media state, or a not-found response.</returns>
    /// <remarks>
    /// The lookup is not site-scoped and no physical file is removed. The commit precedes event
    /// publication, so a bus failure can escape after deletion has succeeded.
    /// </remarks>
public async Task<AeroRequestResponse<MediaViewModel>> DeleteMediaAsync(long id, CancellationToken ct = default)
    {
        await using var session = await _store.LightweightSessionAsync();
        var existing = await session.LoadAsync<MediaAsset>(id, ct);
        if (existing is null)
            return NotFound($"Media {id} not found");
        session.Delete(existing);
        await session.SaveChangesAsync(ct);
        await _bus.PublishAsync(new MediaViewModelDeleted(MapToViewModel(existing)));
        return Ok(MapToViewModel(existing));
    }

    // ── Helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Converts a name to a lowercase, space-delimited slug.
    /// </summary>
    /// <returns>The generated slug; only spaces and one occurrence of double dashes are normalized.</returns>
    private static string GenerateSlug(string name) =>
        name.ToLowerInvariant().Replace(' ', '-').Replace("--", "-");

    /// <summary>
    /// Wraps one media model in a successful actor response.
    /// </summary>
    /// <returns>A response with an empty error model.</returns>
    private static AeroRequestResponse<MediaViewModel> Ok(MediaViewModel vm)
        => new(vm, new MediaErrorViewModel());
    /// <summary>
    /// Adapts a list to the single-value actor response shape.
    /// </summary>
    /// <returns>A response containing the first item, or a new default model for an empty list.</returns>
    private static AeroRequestResponse<MediaViewModel> Ok(IReadOnlyList<MediaViewModel> list)
    {
        var primary = list.Count > 0 ? list[0] : new MediaViewModel();
        return new AeroRequestResponse<MediaViewModel>(primary, new MediaErrorViewModel());
    }
    /// <summary>
    /// Creates a not-found actor response.
    /// </summary>
    /// <returns>A response with a default data model and the supplied error message.</returns>
    private static AeroRequestResponse<MediaViewModel> NotFound(string msg)
        => new(new MediaViewModel(), new MediaErrorViewModel { Message = msg });
    /// <summary>
    /// Creates a general failed actor response.
    /// </summary>
    /// <returns>A response with a default data model and the supplied error message.</returns>
    private static AeroRequestResponse<MediaViewModel> Fail(string msg)
        => new(new MediaViewModel(), new MediaErrorViewModel { Message = msg });

    /// <summary>
    /// Copies persisted media fields into the actor-facing view model.
    /// </summary>
    /// <returns>A detached view model.</returns>
    private static MediaViewModel MapToViewModel(MediaAsset a) => new()
    {
        Id = a.Id, SiteId = a.SiteId, Title = a.FileName, FileName = a.FileName,
        Url = a.Url, MimeType = a.MimeType, FileSizeInBytes = a.FileSize,
        AltText = a.AltText, Description = a.Description, IsFolder = a.IsFolder,
        ParentId = a.ParentId, Dimensions = (a.Width, a.Height),
        CreatedOn = a.CreatedOn, ModifiedOn = a.ModifiedOn,
        CreatedBy = a.CreatedBy, ModifiedBy = a.ModifiedBy
    };
}
