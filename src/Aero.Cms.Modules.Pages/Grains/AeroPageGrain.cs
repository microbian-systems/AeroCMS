using Aero.Actors;
using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Layout;
using Aero.Cms.Abstractions.Blocks.Serialization;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Events;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Abstractions.Requests;
using Aero.Cms.Core.Entities;
using Aero.Core;
using Aero.Core.Extensions;
using Marten;
using Microsoft.Extensions.Logging;
using Wolverine;
using IRequest = Aero.Core.Commands.IRequest;

namespace Aero.Cms.Modules.Pages.Grains;

/// <summary>
/// Orleans grain for page management — wraps Marten persistence + event sourcing
/// behind the <see cref="IAeroPageActor"/> interface.
///
/// Ported from <see cref="MartenPageContentService"/> and <see cref="PagesApi"/> publish/draft handlers.
/// </summary>
public sealed class AeroPageGrain : AeroActor, IAeroPageActor
{
    private readonly IDocumentStore _store;
    private readonly IMessageBus _bus;
    private PageViewModel _state = new();

    public AeroPageGrain(
        ILogger<AeroActor> log,
        IDocumentStore store,
        IMessageBus bus)
        : base(log)
    {
        _store = store;
        _bus = bus;
    }

    // ── IHaveState<PageViewModel> ────────────────────────────────────

    public Task<PageViewModel> GetStateAsync(CancellationToken ct)
        => Task.FromResult(_state);

    public Task UpdateStateAsync(PageViewModel state, CancellationToken ct)
    {
        _state = state;
        return Task.CompletedTask;
    }

    // ── ICruddable<PageViewModel, long> ──────────────────────────────

    public async Task<AeroRequestResponse<PageViewModel>> GetByIdAsync(long id, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();
        var doc = await session.LoadAsync<PageDocument>(id, ct);

        return doc is not null
            ? Ok(MapToViewModel(doc))
            : NotFound($"Page {id} not found");
    }

    public async Task<AeroRequestResponse<PageViewModel>> GetByIdsAsync(long[] ids, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();
        var docs = await session.Query<PageDocument>()
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(ct);

        var primary = docs.Count > 0 ? MapToViewModel(docs[0]) : new PageViewModel();
        return Ok(primary);
    }

    public async Task<AeroRequestResponse<PageViewModel>> CreateAsync(IRequest request, CancellationToken ct)
    {
        if (request is not CreatePageRequest create)
            return Fail("Expected CreatePageRequest");

        await using var session = _store.LightweightSession();

        var slug = string.IsNullOrEmpty(create.Slug)
            ? create.Title.GenerateSlug()
            : create.Slug;

        var page = new PageDocument
        {
            Id = Snowflake.NewId(),
            SiteId = create.SiteId,
            Title = create.Title,
            Slug = slug,
            Summary = create.Summary,
            SeoTitle = create.SeoTitle,
            SeoDescription = create.SeoDescription,
            PublicationState = create.PublicationState,
            ParentId = create.ParentId,
            ShowInNavMenu = create.ShowInNavMenu,
            ShowHeaderNavigation = create.ShowHeaderNavigation,
            HideFooter = create.HideFooter,
            ShowChatAgent = create.ShowChatAgent,
            Blocks = DeserializeEditorBlocks(create.EditorBlocksJson) ?? [],
            LayoutRegions = DeserializeLayoutRegions(create.LayoutRegionsJson) ?? []
        };

        // Compute hierarchy
        var depth = 0;
        var order = 0;
        string path = "/" + page.Slug;

        if (page.ParentId is not null and > 0)
        {
            var parent = await session.LoadAsync<PageDocument>(page.ParentId.Value, ct);
            if (parent is not null)
            {
                path = parent.Path.TrimEnd('/') + "/" + page.Slug;
                depth = parent.Depth + 1;

                var lastSibling = await session.Query<PageDocument>()
                    .Where(x => x.SiteId == page.SiteId && x.ParentId == page.ParentId)
                    .OrderByDescending(x => x.Order)
                    .FirstOrDefaultAsync(ct);
                if (lastSibling is not null)
                    order = lastSibling.Order + 1;
            }
        }

        page.Path = path;
        page.Depth = depth;
        page.Order = order;

        // Reserve slug for public URL routing
        var publicSlug = page.Path.TrimStart('/');
        await ContentSlugReservation.ReserveAsync(
            session, page.Id, ContentSlugOwnerType.Page,
            publicSlug, page.SiteId, previousSlug: null, cancellationToken: ct);

        // Event sourcing — start stream + content update
        session.Events.StartStream($"page-{page.Id}",
            new PageCreated(page.SiteId, page.Title, page.Slug,
                page.ParentId, order, path, depth, page.PublicationState, page.Kind));

        session.Events.Append($"page-{page.Id}", new PageContentUpdated(
            page.Title, page.Slug, page.Summary, page.SeoTitle, page.SeoDescription,
            page.LayoutRegions, page.Blocks,
            Kind: page.Kind,
            ShowHeaderNavigation: page.ShowHeaderNavigation,
            HeaderImageUrl: page.HeaderImageUrl,
            HideHeader: page.HideHeader,
            HideFooter: page.HideFooter,
            ShowChatAgent: page.ShowChatAgent,
            BlockIdMap: page.BlockIdMap));

        await session.SaveChangesAsync(ct);

        var vm = MapToViewModel(page);
        await _bus.PublishAsync(new PageViewModelCreated(vm, $"Page created: {page.Title}"));
        await _bus.PublishAsync(new PageContentUpdatedEvent(page.Id, page.SiteId, page.Slug, null));

        return Ok(vm);
    }

    public async Task<AeroRequestResponse<PageViewModel>> UpdateAsync(IRequest request, CancellationToken ct)
    {
        if (request is not UpdatePageRequest update)
            return Fail("Expected UpdatePageRequest");

        await using var session = _store.LightweightSession();
        var page = await session.LoadAsync<PageDocument>(update.Id, ct);

        if (page is null)
            return NotFound($"Page {update.Id} not found");

        var oldSlug = page.Slug;

        page.Title = update.Title;
        page.Slug = string.IsNullOrEmpty(update.Slug) ? page.Slug : update.Slug;
        page.Summary = update.Summary;
        page.SeoTitle = update.SeoTitle;
        page.SeoDescription = update.SeoDescription;
        page.PublicationState = update.PublicationState;
        page.ShowInNavMenu = update.ShowInNavMenu;
        page.ShowHeaderNavigation = update.ShowHeaderNavigation;
        page.HideFooter = update.HideFooter;
        page.ShowChatAgent = update.ShowChatAgent;
        page.ModifiedOn = DateTimeOffset.UtcNow;

        // EditorBlocksJson: null = omitted (preserve existing); non-null = apply (empty string clears)
        if (update.EditorBlocksJson is not null)
            page.Blocks = DeserializeEditorBlocks(update.EditorBlocksJson) ?? [];
        // LayoutRegionsJson: null = omitted; non-null = apply
        if (update.LayoutRegionsJson is not null)
            page.LayoutRegions = DeserializeLayoutRegions(update.LayoutRegionsJson) ?? [];

        // Append event to stream
        session.Events.Append($"page-{page.Id}", new PageContentUpdated(
            page.Title, page.Slug, page.Summary, page.SeoTitle, page.SeoDescription,
            page.LayoutRegions, page.Blocks,
            Kind: page.Kind,
            ShowHeaderNavigation: page.ShowHeaderNavigation,
            HeaderImageUrl: page.HeaderImageUrl,
            HideHeader: page.HideHeader,
            HideFooter: page.HideFooter,
            ShowChatAgent: page.ShowChatAgent,
            BlockIdMap: page.BlockIdMap));

        await session.SaveChangesAsync(ct);

        var vm = MapToViewModel(page);
        await _bus.PublishAsync(new PageViewModelUpdated(vm, $"Page updated: {page.Title}"));
        await _bus.PublishAsync(new PageContentUpdatedEvent(page.Id, page.SiteId, page.Slug, oldSlug));

        return Ok(vm);
    }

    public async Task<AeroRequestResponse<PageViewModel>> DeleteAsync(IRequest request, CancellationToken ct)
    {
        if (request is not DeletePageRequest delete)
            return Fail("Expected DeletePageRequest");

        await using var session = _store.LightweightSession();
        var page = await session.LoadAsync<PageDocument>(delete.Id, ct);

        if (page is null)
            return NotFound($"Page {delete.Id} not found");

        // Append delete event + soft-delete via Marten
        session.Events.Append($"page-{page.Id}", new PageDeleted("API delete"));
        await session.SaveChangesAsync(ct);

        var vm = MapToViewModel(page);
        await _bus.PublishAsync(new PageViewModelDeleted(vm, $"Page deleted: {page.Title}"));
        await _bus.PublishAsync(new PageContentUpdatedEvent(page.Id, page.SiteId, page.Slug, page.Slug));

        return Ok(vm);
    }

    // ── ICanFindBySite<PageViewModel, long> ──────────────────────────

    public async Task<AeroRequestResponse<PageViewModel>> GetBySiteIdAsync(
        long siteId, int page = 1, int rows = 10, CancellationToken ct = default)
    {
        await using var session = _store.LightweightSession();

        var docs = await session.Query<PageDocument>()
            .Where(x => x.SiteId == siteId)
            .OrderBy(x => x.Order)
            .Skip((page - 1) * rows)
            .Take(rows)
            .ToListAsync(ct);

        var primary = docs.Count > 0 ? MapToViewModel(docs[0]) : new PageViewModel();
        return Ok(primary);
    }

    // ── ICanFindBySlug ──────────────────────────────────────────────

    public Task<AeroRequestResponse<PageViewModel>> GetBySlugAsync(long siteId, string slug, CancellationToken ct)
        => GetBySlugCoreAsync(siteId, slug, ct);

    Task<AeroRequestResponse<PageViewModel>> ICanFindBySlug<PageViewModel, string>.GetBySlugAsync(string siteId, string slug, CancellationToken ct)
    {
        if (long.TryParse(siteId, out var id))
            return GetBySlugCoreAsync(id, slug, ct);
        return Task.FromResult(Fail($"Invalid site ID: {siteId}"));
    }

    private async Task<AeroRequestResponse<PageViewModel>> GetBySlugCoreAsync(long siteId, string slug, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();
        var normalized = ContentSlugDocument.Normalize(slug);

        // Check slug reservation
        var reservation = await session.Query<ContentSlugDocument>()
            .FirstOrDefaultAsync(x =>
                x.SiteId == siteId &&
                string.Equals(normalized, x.NormalizedSlug, StringComparison.OrdinalIgnoreCase), ct);

        if (reservation is not null && reservation.OwnerType == ContentSlugOwnerType.Page)
        {
            var doc = await session.LoadAsync<PageDocument>(reservation.OwnerId, ct);
            if (doc is not null)
                return Ok(MapToViewModel(doc));
        }

        // Fallback: direct Path lookup
        var pathToMatch = "/" + normalized;
        var directPage = await session.Query<PageDocument>()
            .FirstOrDefaultAsync(x =>
                x.SiteId == siteId &&
                string.Equals(pathToMatch, x.Path, StringComparison.OrdinalIgnoreCase), ct);

        return directPage is not null
            ? Ok(MapToViewModel(directPage))
            : NotFound($"Page with slug '{slug}' not found");
    }

    // ── IAeroPageActor page-specific methods ───────────────────────────

    public async Task<(List<PageViewModel> Items, long TotalCount)> GetAllPagesAsync(
        int skip, int take, string? search, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();

        var martenQuery = session.Query<PageDocument>();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            martenQuery = (global::Marten.Linq.IMartenQueryable<PageDocument>)martenQuery
                .Where(x => x.Title.ToLower().Contains(s) || x.Slug.ToLower().Contains(s));
        }

        var totalCount = await martenQuery.CountAsync(ct);
        var pages = await martenQuery
            .OrderBy(x => x.Title)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

        return (pages.Select(MapToViewModel).ToList(), totalCount);
    }

    public async Task<AeroRequestResponse<PageViewModel>> PublishAsync(long id, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();
        var page = await session.LoadAsync<PageDocument>(id, ct);

        if (page is null)
            return NotFound($"Page {id} not found");

        session.Events.Append($"page-{id}", new PageStateChanged(ContentPublicationState.Published));
        await session.SaveChangesAsync(ct);

        // Publish cache-invalidation event so the OutputCache "PagesPolicy"
        // (tagged "pages-list") and FusionCache entries are evicted.
        // Without this, CDN/browser caches may serve stale page content
        // after a publish action. The ContentUpdatedHandler picks this up
        // and invalidates both cache layers in a single handler call.
        await _bus.PublishAsync(new PageContentUpdatedEvent(page.Id, page.SiteId, page.Slug, page.Slug));

        return Ok(MapToViewModel(page));
    }

    public async Task<AeroRequestResponse<PageViewModel>> UnpublishAsync(long id, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();
        var page = await session.LoadAsync<PageDocument>(id, ct);

        if (page is null)
            return NotFound($"Page {id} not found");

        session.Events.Append($"page-{id}", new PageStateChanged(ContentPublicationState.Draft));
        await session.SaveChangesAsync(ct);

        // Same cache eviction as PublishAsync — unpublishing changes
        // the visible state of the page, so cached copies must be evicted.
        await _bus.PublishAsync(new PageContentUpdatedEvent(page.Id, page.SiteId, page.Slug, page.Slug));

        return Ok(MapToViewModel(page));
    }

    public async Task<int> DeleteMultipleAsync(long[] ids, bool deleteDescendants, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();
        var count = 0;

        foreach (var id in ids)
        {
            var page = await session.LoadAsync<PageDocument>(id, ct);
            if (page is not null)
            {
                session.Events.Append($"page-{page.Id}", new PageDeleted("Bulk delete"));
                count++;
            }
        }

        await session.SaveChangesAsync(ct);
        return count;
    }

    public async Task<List<PageEventItem>> GetEventHistoryAsync(long id, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();

        var streamKey = $"page-{id}";
        var page = await session.LoadAsync<PageDocument>(id, ct);

        if (page is null)
            return [];

        var events = await session.Events.FetchStreamAsync(streamKey, token: ct);

        return events.Select(e => new PageEventItem(
            e.Version,
            e.EventType.Name,
            e.Timestamp,
            e.StreamKey ?? streamKey,
            e.IsArchived
        )).ToList();
    }

    // ── AeroRequestResponse helpers ────────────────────────────────────

    private static AeroRequestResponse<PageViewModel> Ok(PageViewModel vm)
        => new(vm, new PageErrorViewModel());

    private static AeroRequestResponse<PageViewModel> NotFound(string msg)
        => new(new PageViewModel(), new PageErrorViewModel { Message = msg });

    private static AeroRequestResponse<PageViewModel> Fail(string msg)
        => new(new PageViewModel(), new PageErrorViewModel { Message = msg });

    // ── Orleans transport helpers ──────────────────────────────────────

    /// <summary>
    /// Deserializes <see cref="EditorBlock"/> list from JSON string (Orleans-safe transport).
    /// Returns null when Json is null (omitted), empty list when Json is empty string.
    /// </summary>
    private static List<EditorBlock>? DeserializeEditorBlocks(string? json)
    {
        if (json is null)
            return null;

        if (json.Length == 0)
            return [];

        return System.Text.Json.JsonSerializer.Deserialize<List<EditorBlock>>(
            json, BlockJsonContext.Default.Options);
    }

    private static List<LayoutRegion>? DeserializeLayoutRegions(string? json)
    {
        if (json is null)
            return null;

        if (json.Length == 0)
            return [];

        return System.Text.Json.JsonSerializer.Deserialize<List<LayoutRegion>>(
            json, BlockJsonContext.Default.Options);
    }

    // ── Mapping ───────────────────────────────────────────────────────

    private static PageViewModel MapToViewModel(PageDocument doc) => new()
    {
        Id = doc.Id,
        SiteId = doc.SiteId,
        Title = doc.Title,
        Slug = doc.Slug,
        Kind = doc.Kind,
        Summary = doc.Summary,
        SeoTitle = doc.SeoTitle,
        SeoDescription = doc.SeoDescription,
        PublishedOn = doc.PublishedOn,
        IsPublished = doc.PublicationState == ContentPublicationState.Published,
        ParentId = doc.ParentId,
        Path = doc.Path,
        Depth = doc.Depth,
        Order = doc.Order,
        IsHidden = doc.IsHidden,
        ShowInNavMenu = doc.ShowInNavMenu
    };
}
