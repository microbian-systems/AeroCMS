using Aero.Actors;
using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Enums;
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

namespace Aero.Cms.Modules.Docs.Grains;

/// <summary>
/// Orleans grain for docs management — wraps Marten persistence behind
/// the <see cref="IAeroDocsActor"/> interface.
///
/// Follows the Marten integration pattern: <see cref="IDocumentStore"/> as a
/// singleton, lightweight session per operation.
///
/// Publishes Wolverine events after each mutation for cache invalidation and
/// downstream workflows.
/// </summary>
public sealed class AeroDocsGrain : AeroActor, IAeroDocsActor
{
    private readonly IDocumentStore _store;
    private readonly IMessageBus _bus;
    private DocViewModel _state = new();

    public AeroDocsGrain(
        ILogger<AeroActor> log,
        IDocumentStore store,
        IMessageBus bus)
        : base(log)
    {
        _store = store;
        _bus = bus;
    }

    // ── IHaveState<DocViewModel> ────────────────────────────────────

    public Task<DocViewModel> GetStateAsync(CancellationToken ct)
        => Task.FromResult(_state);

    public Task UpdateStateAsync(DocViewModel state, CancellationToken ct)
    {
        _state = state;
        return Task.CompletedTask;
    }

    // ── ICruddable<DocViewModel, long> ──────────────────────────────

    public async Task<AeroRequestResponse<DocViewModel>> GetByIdAsync(long id, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();
        var doc = await session.LoadAsync<DocsPage>(id, ct);

        return doc is not null
            ? Ok(MapToViewModel(doc))
            : NotFound($"Doc {id} not found");
    }

    public async Task<AeroRequestResponse<DocViewModel>> GetByIdsAsync(long[] ids, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();
        var docs = await session.Query<DocsPage>()
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(ct);

        // ICruddable returns single T; return first as canonical
        var primary = docs.Count > 0 ? MapToViewModel(docs[0]) : new DocViewModel();
        return Ok(primary);
    }

    public async Task<AeroRequestResponse<DocViewModel>> CreateAsync(IRequest request, CancellationToken ct)
    {
        if (request is not CreateDocRequest create)
            return Fail("Expected CreateDocRequest");

        await using var session = _store.LightweightSession();

        var doc = new DocsPage
        {
            Id = Snowflake.NewId(),
            SiteId = create.SiteId,
            Title = create.Title,
            Slug = create.Slug,
            Summary = create.Summary,
            SeoTitle = create.SeoTitle,
            SeoDescription = create.SeoDescription,
            MarkdownContent = create.Markdown ?? create.Content,
            PublicationState = create.PublicationState
        };

        session.Store(doc);
        await session.SaveChangesAsync(ct);

        var vm = MapToViewModel(doc);
        await _bus.PublishAsync(new DocViewModelCreated(vm, $"Doc created: {doc.Slug}"));
        await _bus.PublishAsync(new DocsPageContentUpdatedEvent(doc.Id, doc.SiteId, doc.Slug, null));

        return Ok(vm);
    }

    public async Task<AeroRequestResponse<DocViewModel>> UpdateAsync(IRequest request, CancellationToken ct)
    {
        if (request is not UpdateDocRequest update)
            return Fail("Expected UpdateDocRequest");

        await using var session = _store.LightweightSession();
        var doc = await session.LoadAsync<DocsPage>(update.Id, ct);

        if (doc is null)
            return NotFound($"Doc {update.Id} not found");

        var oldSlug = doc.Slug;
        doc.Title = update.Title;
        doc.Slug = update.Slug;
        doc.Summary = update.Summary;
        doc.SeoTitle = update.SeoTitle;
        doc.SeoDescription = update.SeoDescription;
        doc.MarkdownContent = update.Markdown ?? update.Content;
        doc.PublicationState = update.PublicationState;
        doc.ModifiedOn = DateTimeOffset.UtcNow;

        session.Store(doc);
        await session.SaveChangesAsync(ct);

        var vm = MapToViewModel(doc);
        await _bus.PublishAsync(new DocViewModelUpdated(vm, $"Doc updated: {doc.Slug}"));
        await _bus.PublishAsync(new DocsPageContentUpdatedEvent(doc.Id, doc.SiteId, doc.Slug, oldSlug));

        return Ok(vm);
    }

    public async Task<AeroRequestResponse<DocViewModel>> DeleteAsync(IRequest request, CancellationToken ct)
    {
        if (request is not DeleteDocRequest delete)
            return Fail("Expected DeleteDocRequest");

        await using var session = _store.LightweightSession();
        var doc = await session.LoadAsync<DocsPage>(delete.Id, ct);

        if (doc is null)
            return NotFound($"Doc {delete.Id} not found");

        session.Delete(doc);
        await session.SaveChangesAsync(ct);

        var vm = MapToViewModel(doc);
        await _bus.PublishAsync(new DocViewModelDeleted(vm, $"Doc deleted: {doc.Slug}"));
        await _bus.PublishAsync(new DocsPageContentUpdatedEvent(doc.Id, doc.SiteId, doc.Slug, doc.Slug));

        return Ok(vm);
    }

    // ── ICanFindBySite<DocViewModel, long> ──────────────────────────

    public async Task<AeroRequestResponse<DocViewModel>> GetBySiteIdAsync(
        long siteId,
        int page = 1,
        int rows = 10,
        CancellationToken ct = default)
    {
        await using var session = _store.LightweightSession();

        var docs = await session.Query<DocsPage>()
            .Where(x => x.SiteId == siteId)
            .OrderBy(x => x.Order)
            .Skip((page - 1) * rows)
            .Take(rows)
            .ToListAsync(ct);

        var primary = docs.Count > 0 ? MapToViewModel(docs[0]) : new DocViewModel();
        return Ok(primary);
    }

    // ── ICanFindBySlug (long key + string key overloads) ──────────────

    public Task<AeroRequestResponse<DocViewModel>> GetBySlugAsync(long siteId, string slug, CancellationToken ct)
        => GetBySlugCoreAsync(siteId, slug, ct);

    Task<AeroRequestResponse<DocViewModel>> ICanFindBySlug<DocViewModel, string>.GetBySlugAsync(string siteId, string slug, CancellationToken ct)
    {
        if (long.TryParse(siteId, out var id))
            return GetBySlugCoreAsync(id, slug, ct);

        return Task.FromResult(Fail($"Invalid site ID: {siteId}"));
    }

    private async Task<AeroRequestResponse<DocViewModel>> GetBySlugCoreAsync(long siteId, string slug, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();

        var doc = await session.Query<DocsPage>()
            .FirstOrDefaultAsync(x => x.SiteId == siteId && x.Slug == slug, ct);

        return doc is not null
            ? Ok(MapToViewModel(doc))
            : NotFound($"Doc with slug '{slug}' not found");
    }

    // ── IAeroDocsActor doc-specific methods ───────────────────────────

    public async Task<List<DocViewModel>> GetAllBySiteAsync(long siteId, CancellationToken ct = default)
    {
        await using var session = _store.LightweightSession();

        var docs = await session.Query<DocsPage>()
            .Where(x => x.SiteId == siteId)
            .OrderBy(x => x.Order)
            .ToListAsync(ct);

        return docs.Select(MapToViewModel).ToList();
    }

    public async Task<List<DocViewModel>> GetChildrenAsync(long parentId, long siteId, CancellationToken ct = default)
    {
        await using var session = _store.LightweightSession();

        var children = await session.Query<DocsPage>()
            .Where(x => x.SiteId == siteId && x.ParentId == parentId)
            .OrderBy(x => x.Order)
            .ToListAsync(ct);

        return children.Select(MapToViewModel).ToList();
    }

    public async Task<List<DocViewModel>> GetTopLevelCategoriesAsync(long siteId, CancellationToken ct = default)
    {
        await using var session = _store.LightweightSession();

        // Find root "docs" page
        var rootDoc = await session.Query<DocsPage>()
            .FirstOrDefaultAsync(x => x.SiteId == siteId && x.Slug == "docs", ct);

        if (rootDoc is null)
            return [];

        var children = await session.Query<DocsPage>()
            .Where(x => x.SiteId == siteId && x.ParentId == rootDoc.Id)
            .OrderBy(x => x.Order)
            .ToListAsync(ct);

        return children.Select(MapToViewModel).ToList();
    }

    public async Task<AeroRequestResponse<DocViewModel>> SaveAsync(DocViewModel vm, CancellationToken ct = default)
    {
        await using var session = _store.LightweightSession();

        var existing = await session.LoadAsync<DocsPage>(vm.Id, ct);
        var isNew = existing is null;
        var oldSlug = existing?.Slug;

        var doc = isNew
            ? new DocsPage { Id = Snowflake.NewId() }
            : existing!;

        doc.SiteId = vm.SiteId;
        doc.Title = vm.Title ?? string.Empty;
        doc.Slug = vm.Slug ?? string.Empty;
        doc.Summary = vm.Summary;
        doc.MarkdownContent = vm.MarkdownContent;
        doc.SeoTitle = vm.SeoTitle;
        doc.SeoDescription = vm.SeoDescription;
        doc.PublicationState = vm.PublicationState;
        doc.PublishedOn = vm.PublishedOn;
        doc.ShowHeaderNavigation = vm.ShowHeaderNavigation;
        doc.HeaderImageUrl = vm.HeaderImageUrl;
        doc.ParentId = vm.ParentId;
        doc.Order = vm.Order;
        doc.ModifiedOn = DateTimeOffset.UtcNow;

        session.Store(doc);
        await session.SaveChangesAsync(ct);

        var result = MapToViewModel(doc);

        if (isNew)
            await _bus.PublishAsync(new DocViewModelCreated(result, $"Doc created: {doc.Slug}"));
        else
            await _bus.PublishAsync(new DocViewModelUpdated(result, $"Doc updated: {doc.Slug}"));

        await _bus.PublishAsync(new DocsPageContentUpdatedEvent(doc.Id, doc.SiteId, doc.Slug, oldSlug));

        return Ok(result);
    }

    // ── AeroRequestResponse helpers ────────────────────────────────────

    private static AeroRequestResponse<DocViewModel> Ok(DocViewModel vm)
        => new(vm, new DocErrorViewModel());

    private static AeroRequestResponse<DocViewModel> NotFound(string msg)
        => new(new DocViewModel(), new DocErrorViewModel { Message = msg });

    private static AeroRequestResponse<DocViewModel> Fail(string msg)
        => new(new DocViewModel(), new DocErrorViewModel { Message = msg });

    // ── Mapping ───────────────────────────────────────────────────────

    private static DocViewModel MapToViewModel(DocsPage doc) => new()
    {
        Id = doc.Id,
        SiteId = doc.SiteId,
        Slug = doc.Slug,
        Title = doc.Title,
        Summary = doc.Summary,
        MarkdownContent = doc.MarkdownContent,
        SeoTitle = doc.SeoTitle,
        SeoDescription = doc.SeoDescription,
        PublicationState = doc.PublicationState,
        PublishedOn = doc.PublishedOn,
        ShowHeaderNavigation = doc.ShowHeaderNavigation,
        HeaderImageUrl = doc.HeaderImageUrl,
        ParentId = doc.ParentId,
        Order = doc.Order,
        CreatedOn = doc.CreatedOn,
        ModifiedOn = doc.ModifiedOn,
        CreatedBy = doc.CreatedBy,
        ModifiedBy = doc.ModifiedBy
    };
}
