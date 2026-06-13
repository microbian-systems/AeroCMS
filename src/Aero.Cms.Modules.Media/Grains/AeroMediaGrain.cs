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
/// Orleans grain for media asset management — wraps Marten persistence.
/// Publishes Wolverine events after mutations.
/// File I/O (disk writes) remains in the API layer — grain handles persistence only.
/// </summary>
public sealed class AeroMediaGrain : AeroActor, IAeroMediaActor
{
    private readonly IDocumentStore _store;
    private readonly IMessageBus _bus;
    private MediaViewModel _state = new();

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

    public Task<MediaViewModel> GetStateAsync(CancellationToken ct)
        => Task.FromResult(_state);

    public Task UpdateStateAsync(MediaViewModel state, CancellationToken ct)
    {
        _state = state;
        return Task.CompletedTask;
    }

    // ── ICruddable<MediaViewModel, long> ──────────────────────────────

    public async Task<AeroRequestResponse<MediaViewModel>> GetByIdAsync(long id, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();
        var asset = await session.LoadAsync<MediaAsset>(id, ct);
        return asset is not null
            ? Ok(MapToViewModel(asset))
            : NotFound($"Media {id} not found");
    }

    public async Task<AeroRequestResponse<MediaViewModel>> GetByIdsAsync(long[] ids, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();
        var assets = await session.Query<MediaAsset>()
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(ct);
        var results = assets.Select(MapToViewModel).ToList();
        return Ok(results);
    }

    // ICruddable stubs — use SaveMediaAsync / DeleteMediaAsync instead
    public Task<AeroRequestResponse<MediaViewModel>> CreateAsync(IRequest request, CancellationToken ct)
        => Task.FromResult(Fail("Use SaveMediaAsync"));
    public Task<AeroRequestResponse<MediaViewModel>> UpdateAsync(IRequest request, CancellationToken ct)
        => Task.FromResult(Fail("Use SaveMediaAsync"));
    public Task<AeroRequestResponse<MediaViewModel>> DeleteAsync(IRequest request, CancellationToken ct)
        => Task.FromResult(Fail("Use DeleteMediaAsync"));

    // ── ICanFindBySite ────────────────────────────────────────────────

    public async Task<AeroRequestResponse<MediaViewModel>> GetBySiteIdAsync(
        long siteId, int page = 1, int rows = 10, CancellationToken ct = default)
    {
        await using var session = _store.LightweightSession();
        var assets = await session.Query<MediaAsset>()
            .Where(x => x.SiteId == siteId)
            .OrderBy(x => x.FileName)
            .Skip((page - 1) * rows).Take(rows)
            .ToListAsync(ct);
        var results = assets.Select(MapToViewModel).ToList();
        return Ok(results);
    }

    // ── ICanFindBySlug ────────────────────────────────────────────────

    public Task<AeroRequestResponse<MediaViewModel>> GetBySlugAsync(long siteId, string slug, CancellationToken ct)
        => GetBySlugCoreAsync(siteId, slug, ct);

    Task<AeroRequestResponse<MediaViewModel>> ICanFindBySlug<MediaViewModel, string>.GetBySlugAsync(string siteId, string slug, CancellationToken ct)
    {
        if (long.TryParse(siteId, out var id))
            return GetBySlugCoreAsync(id, slug, ct);
        return Task.FromResult(Fail($"Invalid site ID: {siteId}"));
    }

    private async Task<AeroRequestResponse<MediaViewModel>> GetBySlugCoreAsync(long siteId, string slug, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();
        var assets = await session.Query<MediaAsset>()
            .Where(x => x.SiteId == siteId && x.Url != null && x.Url.Contains(slug))
            .ToListAsync(ct);
        var results = assets.Select(MapToViewModel).ToList();
        return Ok(results);
    }

    // ── IAeroMediaActor custom methods ────────────────────────────────

    public async Task<List<MediaViewModel>> GetAllAsync(CancellationToken ct = default)
    {
        await using var session = _store.LightweightSession();
        var assets = await session.Query<MediaAsset>()
            .OrderBy(x => x.FileName)
            .ToListAsync(ct);
        return assets.Select(MapToViewModel).ToList();
    }

    public async Task<(List<MediaViewModel> Items, long TotalCount)> GetPagedAsync(
        long? parentId, int skip, int take, string? search, CancellationToken ct = default)
    {
        await using var session = _store.LightweightSession();
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

    public async Task<AeroRequestResponse<MediaViewModel>> SaveMediaAsync(MediaViewModel vm, CancellationToken ct = default)
    {
        await using var session = _store.LightweightSession();
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

    public async Task<AeroRequestResponse<MediaViewModel>> DeleteMediaAsync(long id, CancellationToken ct = default)
    {
        await using var session = _store.LightweightSession();
        var existing = await session.LoadAsync<MediaAsset>(id, ct);
        if (existing is null)
            return NotFound($"Media {id} not found");
        session.Delete(existing);
        await session.SaveChangesAsync(ct);
        await _bus.PublishAsync(new MediaViewModelDeleted(MapToViewModel(existing)));
        return Ok(MapToViewModel(existing));
    }

    // ── Helpers ────────────────────────────────────────────────────────

    private static string GenerateSlug(string name) =>
        name.ToLowerInvariant().Replace(' ', '-').Replace("--", "-");

    private static AeroRequestResponse<MediaViewModel> Ok(MediaViewModel vm)
        => new(vm, new MediaErrorViewModel());
    private static AeroRequestResponse<MediaViewModel> Ok(IReadOnlyList<MediaViewModel> list)
    {
        var primary = list.Count > 0 ? list[0] : new MediaViewModel();
        return new AeroRequestResponse<MediaViewModel>(primary, new MediaErrorViewModel());
    }
    private static AeroRequestResponse<MediaViewModel> NotFound(string msg)
        => new(new MediaViewModel(), new MediaErrorViewModel { Message = msg });
    private static AeroRequestResponse<MediaViewModel> Fail(string msg)
        => new(new MediaViewModel(), new MediaErrorViewModel { Message = msg });

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
