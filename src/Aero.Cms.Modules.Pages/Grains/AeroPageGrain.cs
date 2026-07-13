using System.Text.Json;
using Aero.Actors;
using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Blocks.Layout;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Styles;
using Aero.Cms.Abstractions.Blocks.Serialization;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Events;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Abstractions.Requests;
using Aero.Cms.Html;
using Aero.Core;
using Aero.Core.Http;
using Aero.Core.Railway;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;
using ZiggyCreatures.Caching.Fusion;
using IRequest = Aero.Core.Commands.IRequest;

namespace Aero.Cms.Modules.Pages.Grains;

/// <summary>
/// Orleans grain for page management — opens sessions from <see cref="IDocumentStore"/>,
/// manually constructs <see cref="AeroPageContentService"/> with a <see cref="FixedSiteContext"/>,
/// and delegates each operation to the service.
/// </summary>
public sealed class AeroPageGrain : AeroActor, IAeroPageActor
{
    private readonly IDocumentStore _store;
    private readonly IServiceProvider _services;
    private PageViewModel _state = new();

        /// <summary>
    /// Initializes a new instance of the <see cref="AeroPageGrain"/> class.
    /// </summary>
public AeroPageGrain(
        ILogger<AeroActor> log,
        IDocumentStore store,
        IServiceProvider services)
        : base(log)
    {
        _store = store;
        _services = services;
    }

    // ── Helper: manual construction of AeroPageContentService ────────

    private AeroPageContentService CreatePageService(IDocumentSession session, long siteId)
    {
        var bus = _services.GetRequiredService<IMessageBus>();
        var logger = _services.GetRequiredService<ILogger<AeroPageContentService>>();
        var cache = _services.GetService<IFusionCache>();
        var pageTreeService = _services.GetService<IPageTreeService>();
        var contentValidator = _services.GetRequiredService<IHtmlContentValidator>();
        var styleCompiler = _services.GetRequiredService<IStyleCompiler>();
        var styleProfile = _services.GetRequiredService<IStyleProfile>();
        return new AeroPageContentService(
            session,
            bus,
            new FixedSiteContext(siteId),
            logger,
            contentValidator,
            styleCompiler,
            styleProfile,
            "system",
            cache,
            pageTreeService);
    }

    // ── IHaveState<PageViewModel> ────────────────────────────────────

        /// <summary>
    /// GetStateAsync method.
    /// </summary>
public Task<PageViewModel> GetStateAsync(CancellationToken ct)
        => Task.FromResult(_state);

        /// <summary>
    /// UpdateStateAsync method.
    /// </summary>
public Task UpdateStateAsync(PageViewModel state, CancellationToken ct)
    {
        _state = state;
        return Task.CompletedTask;
    }

    // ── ICruddable<PageViewModel, long> ──────────────────────────────

    /// <summary>
    /// Direct AeroDB load — no siteId available via <see cref="ICruddable{T,TKey}"/>.
    /// </summary>
    public async Task<AeroRequestResponse<PageViewModel>> GetByIdAsync(long id, CancellationToken ct)
    {
        await using var session = await _store.QuerySessionAsync();
        var doc = await session.LoadAsync<PageDocument>(id, ct);

        return doc is not null
            ? Ok(doc.ToViewModel())
            : NotFound($"Page {id} not found");
    }

    /// <summary>
    /// Direct AeroDB query — no siteId available via interface.
    /// </summary>
    public async Task<AeroRequestResponse<PageViewModel>> GetByIdsAsync(long[] ids, CancellationToken ct)
    {
        await using var session = await _store.QuerySessionAsync();
        var docs = await session.Query<PageDocument>()
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(ct);

        var primary = docs.Count > 0 ? docs[0].ToViewModel() : new PageViewModel();
        return Ok(primary);
    }

        /// <summary>
    /// CreateAsync method.
    /// </summary>
public async Task<AeroRequestResponse<PageViewModel>> CreateAsync(IRequest request, CancellationToken ct)
    {
        if (request is not CreatePageRequest create)
            return Fail("Expected CreatePageRequest");

        create = RehydrateTransportPayload(create);

        await using var session = await _store.LightweightSessionAsync();
        var pageService = CreatePageService(session, create.SiteId);
        var result = await pageService.CreateAsync(create, ct);

        if (result is Result<PageDocument, AeroError>.Ok ok)
            return Ok(ok.Value.ToViewModel());
        if (result is Result<PageDocument, AeroError>.Failure fail)
            return Fail(fail.Error.ToString() ?? "Create failed");
        return Fail("Unexpected result");
    }

        /// <summary>
    /// UpdateAsync method.
    /// </summary>
public async Task<AeroRequestResponse<PageViewModel>> UpdateAsync(IRequest request, CancellationToken ct)
    {
        if (request is not UpdatePageRequest update)
            return Fail("Expected UpdatePageRequest");

        update = RehydrateTransportPayload(update);

        // Load page from store to obtain its SiteId (not present on UpdatePageRequest)
        await using var loadSession = await _store.QuerySessionAsync();
        var page = await loadSession.LoadAsync<PageDocument>(update.Id, ct);

        if (page is null)
            return NotFound($"Page {update.Id} not found");

        var siteId = page.SiteId;

        await using var session = await _store.LightweightSessionAsync();
        var pageService = CreatePageService(session, siteId);
        var result = await pageService.UpdateAsync(update.Id, update, ct);

        if (result is Result<PageDocument, AeroError>.Ok ok)
            return Ok(ok.Value.ToViewModel());
        if (result is Result<PageDocument, AeroError>.Failure fail)
            return Fail(fail.Error.ToString() ?? "Update failed");
        return Fail("Unexpected result");
    }

        /// <summary>
    /// DeleteAsync method.
    /// </summary>
public async Task<AeroRequestResponse<PageViewModel>> DeleteAsync(IRequest request, CancellationToken ct)
    {
        if (request is not DeletePageRequest delete)
            return Fail("Expected DeletePageRequest");

        // Load page from store to obtain SiteId and capture the view model
        await using var loadSession = await _store.QuerySessionAsync();
        var page = await loadSession.LoadAsync<PageDocument>(delete.Id, ct);

        if (page is null)
            return NotFound($"Page {delete.Id} not found");

        var siteId = page.SiteId;
        var vm = page.ToViewModel();

        await using var session = await _store.LightweightSessionAsync();
        var pageService = CreatePageService(session, siteId);
        var result = await pageService.DeleteAsync(delete.Id, ct);

        if (result is Result<bool, AeroError>.Ok)
            return Ok(vm);
        if (result is Result<bool, AeroError>.Failure fail)
            return Fail(fail.Error.ToString() ?? "Delete failed");
        return Fail("Unexpected result");
    }

    // ── ICanFindBySite<PageViewModel, long> ──────────────────────────

        /// <summary>
    /// GetBySiteIdAsync method.
    /// </summary>
public async Task<AeroRequestResponse<PageViewModel>> GetBySiteIdAsync(
        long siteId, int page = 1, int rows = 10, CancellationToken ct = default)
    {
        await using var session = await _store.LightweightSessionAsync();
        var pageService = CreatePageService(session, siteId);
        var result = await pageService.GetAllPagesAsync((page - 1) * rows, rows, null, ct);

        if (result is Result<(IReadOnlyList<PageDocument> Items, long TotalCount), AeroError>.Ok(var list))
            return list.Items.Count > 0
                ? Ok(list.Items[0].ToViewModel())
                : Ok(new PageViewModel());
        if (result is Result<(IReadOnlyList<PageDocument> Items, long TotalCount), AeroError>.Failure fail)
            return Fail(fail.Error.ToString() ?? "GetBySiteId failed");
        return Ok(new PageViewModel());
    }

    // ── ICanFindBySlug ──────────────────────────────────────────────

        /// <summary>
    /// GetBySlugAsync method.
    /// </summary>
public Task<AeroRequestResponse<PageViewModel>> GetBySlugAsync(long siteId, string slug, CancellationToken ct)
        => GetBySlugCoreAsync(siteId, slug, culture: null, ct);

        /// <summary>
    /// GetBySlugAsync method.
    /// </summary>
public Task<AeroRequestResponse<PageViewModel>> GetBySlugAsync(long siteId, string slug, string? culture, CancellationToken ct)
        => GetBySlugCoreAsync(siteId, slug, culture, ct);

    Task<AeroRequestResponse<PageViewModel>> ICanFindBySlug<PageViewModel, string>.GetBySlugAsync(
        string siteId, string slug, CancellationToken ct)
    {
        if (long.TryParse(siteId, out var id))
            return GetBySlugCoreAsync(id, slug, culture: null, ct);
        return Task.FromResult(Fail($"Invalid site ID: {siteId}"));
    }

    private async Task<AeroRequestResponse<PageViewModel>> GetBySlugCoreAsync(
        long siteId, string slug, string? culture, CancellationToken ct)
    {
        await using var session = await _store.LightweightSessionAsync();
        var pageService = CreatePageService(session, siteId);
        var result = await pageService.FindBySlugAsync(slug, culture, ct);

        if (result is Result<PageDocument?, AeroError>.Ok { Value: not null } ok)
            return Ok(ok.Value.ToViewModel());
        if (result is Result<PageDocument?, AeroError>.Ok)
            return NotFound($"Page with slug '{slug}' not found");
        if (result is Result<PageDocument?, AeroError>.Failure fail)
            return Fail(fail.Error.ToString() ?? $"Page with slug '{slug}' not found");
        return Fail("Unexpected result");
    }

    // ── IAeroPageActor page-specific methods ───────────────────────────

    /// <summary>
    /// Delegates to <see cref="AeroPageContentService"/> for site-scoped paged query.
    /// </summary>
    public async Task<(List<PageViewModel> Items, long TotalCount)> GetAllPagesAsync(
        long siteId, int skip, int take, string? search, CancellationToken ct)
    {
        await using var session = await _store.LightweightSessionAsync();
        var pageService = CreatePageService(session, siteId);
        var result = await pageService.GetAllPagesAsync(skip, take, search, ct);

        if (result is Result<(IReadOnlyList<PageDocument> Items, long TotalCount), AeroError>.Ok ok)
            return (ok.Value.Items.Select(p => p.ToViewModel()).ToList(), ok.Value.TotalCount);
        return ([], 0);
    }

        /// <summary>
    /// PublishAsync method.
    /// </summary>
public async Task<AeroRequestResponse<PageViewModel>> PublishAsync(long id, CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var workflow = scope.ServiceProvider.GetRequiredService<IPagePublishingWorkflowService>();
        var result = await workflow.PublishNowAsync(id, ct);

        if (result is Result<bool, AeroError>.Failure fail)
            return Fail(fail.Error.ToString() ?? "Publish failed");

        await using var session = await _store.QuerySessionAsync();
        var page = await session.LoadAsync<PageDocument>(id, ct);

        return page is not null
            ? Ok(page.ToViewModel())
            : NotFound($"Page {id} not found");
    }

        /// <summary>
    /// UnpublishAsync method.
    /// </summary>
public async Task<AeroRequestResponse<PageViewModel>> UnpublishAsync(long id, CancellationToken ct)
        => await TogglePublishStateAsync(id, ContentPublicationState.Draft, ct);

        /// <summary>
    /// ListCultureVariantsAsync method.
    /// </summary>
public async Task<List<PageViewModel>> ListCultureVariantsAsync(long id, CancellationToken ct)
    {
        await using var loadSession = await _store.QuerySessionAsync();
        var page = await loadSession.LoadAsync<PageDocument>(id, ct);

        if (page is null)
            return [];

        var TranslationGroupId = page.TranslationGroupId ?? page.Id;

        await using var session = await _store.LightweightSessionAsync();
        var pageService = CreatePageService(session, page.SiteId);
        var result = await pageService.ListCultureVariantsAsync(TranslationGroupId, ct);

        return result is Result<IReadOnlyList<PageDocument>, AeroError>.Ok ok
            ? ok.Value.Select(p => p.ToViewModel()).ToList()
            : [];
    }

        /// <summary>
    /// ForkPageForCultureAsync method.
    /// </summary>
public async Task<AeroRequestResponse<PageViewModel>> ForkPageForCultureAsync(
        long id,
        string culture,
        string slug,
        CancellationToken ct)
    {
        await using var loadSession = await _store.QuerySessionAsync();
        var page = await loadSession.LoadAsync<PageDocument>(id, ct);

        if (page is null)
            return NotFound($"Page {id} not found");

        await using var session = await _store.LightweightSessionAsync();
        var pageService = CreatePageService(session, page.SiteId);
        var result = await pageService.ForkPageForCultureAsync(id, culture, slug, ct);

        if (result is Result<PageDocument, AeroError>.Ok ok)
            return Ok(ok.Value.ToViewModel());
        if (result is Result<PageDocument, AeroError>.Failure fail)
            return Fail(fail.Error.ToString() ?? "Culture fork failed");
        return Fail("Unexpected result");
    }

    private async Task<AeroRequestResponse<PageViewModel>> TogglePublishStateAsync(
        long id, ContentPublicationState state, CancellationToken ct)
    {
        await using var session = await _store.LightweightSessionAsync();
        var page = await session.LoadAsync<PageDocument>(id, ct);

        if (page is null)
            return NotFound($"Page {id} not found");

        var stateChanged = new PageStateChanged(state);
        session.Events.Append($"page-{id}", new object[] { stateChanged });
        await session.SaveChangesAsync(ct);

        page.Apply(stateChanged);
        return Ok(page.ToViewModel());
    }

        /// <summary>
    /// DeleteMultipleAsync method.
    /// </summary>
public async Task<int> DeleteMultipleAsync(long[] ids, bool deleteDescendants, CancellationToken ct)
    {
        if (ids.Length == 0)
            return 0;

        // Load the first page to determine the SiteId for the bulk operation
        await using var loadSession = await _store.QuerySessionAsync();
        var firstPage = await loadSession.LoadAsync<PageDocument>(ids[0], ct);
        if (firstPage is null)
            return 0;

        var siteId = firstPage.SiteId;

        await using var session = await _store.LightweightSessionAsync();
        var pageService = CreatePageService(session, siteId);
        var result = await pageService.DeleteMultipleAsync(ids, deleteDescendants, ct);

        if (result is Result<int, AeroError>.Ok ok)
            return ok.Value;
        return 0;
    }

    /// <summary>
    /// Event history is a direct AeroDB read — no equivalent in the service layer.
    /// </summary>
    public async Task<List<PageEventItem>> GetEventHistoryAsync(long id, CancellationToken ct)
    {
        await using var session = await _store.QuerySessionAsync();

        var streamKey = $"page-{id}";
        var page = await session.LoadAsync<PageDocument>(id, ct);

        if (page is null)
            return [];

        var events = await session.Events.FetchStreamAsync(streamKey, ct: ct);

        return events.Select(e => new PageEventItem(
            e.Version,
            e.EventType.Name,
            e.Timestamp,
            e.StreamId.Value ?? streamKey,
            e.Data is PageArchived
        )).ToList();
    }

    // ── AeroRequestResponse helpers ────────────────────────────────────

    private static AeroRequestResponse<PageViewModel> Ok(PageViewModel vm)
        => new(vm, new PageErrorViewModel());

    private static AeroRequestResponse<PageViewModel> NotFound(string msg)
        => new(new PageViewModel(), new PageErrorViewModel { Message = msg });

    private static AeroRequestResponse<PageViewModel> Fail(string msg)
        => new(new PageViewModel(), new PageErrorViewModel { Message = msg });

    private static CreatePageRequest RehydrateTransportPayload(CreatePageRequest request)
    {
        var layoutRegions = request.LayoutRegions
            ?? DeserializeList<LayoutRegion>(request.LayoutRegionsJson);

        return request with
        {
            LayoutRegions = layoutRegions
        };
    }

    private static UpdatePageRequest RehydrateTransportPayload(UpdatePageRequest request)
    {
        var layoutRegions = request.LayoutRegions
            ?? DeserializeList<LayoutRegion>(request.LayoutRegionsJson);

        return request with
        {
            LayoutRegions = layoutRegions
        };
    }

    private static List<T>? DeserializeList<T>(string? json)
        => json is null
            ? null
            : JsonSerializer.Deserialize<List<T>>(json, BlockJsonContext.Default.Options);

    // ── FixedSiteContext ─────────────────────────────────────────────

    private sealed class FixedSiteContext(long siteId) : ISiteContext
    {
                /// <summary>
        /// Gets or sets the Site Id.
        /// </summary>
public long SiteId { get; } = siteId;
                /// <summary>
        /// Gets or sets the Tenant Id.
        /// </summary>
public long TenantId { get; } = siteId;
    }
}
