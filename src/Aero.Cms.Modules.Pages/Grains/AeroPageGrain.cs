using Aero.Actors;
using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Abstractions.Requests;
using Aero.Cms.Html;
using Aero.Cms.Services;
using Aero.Core;
using Aero.Core.Http;
using Aero.Core.Railway;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;
using ZiggyCreatures.Caching.Fusion;
using IRequest = Aero.Core.Commands.IRequest;
using PageRouteChangeImpact = Aero.Cms.Abstractions.Http.Clients.PageRouteChangeImpact;

namespace Aero.Cms.Modules.Pages.Grains;

/// <summary>
/// Orleans grain for page management — opens sessions from <see cref="IDocumentStore"/>,
/// manually constructs <see cref="AeroPageContentService"/> with a <see cref="FixedSiteContext"/>,
/// and delegates each operation to the service.
/// </summary>
/// <remarks>
/// Administrative operations accept an explicit authorized site identifier. Inherited
/// identifier-only CRUD methods fail closed because they cannot establish a tenant
/// boundary. The in-memory state exposed by <c>IHaveState</c> is not persisted by this grain.
/// </remarks>
public sealed class AeroPageGrain : AeroActor, IAeroPageActor
{
    private readonly IDocumentStore _store;
    private readonly IServiceProvider _services;
    private PageViewModel _state = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="AeroPageGrain"/> class.
    /// </summary>
    /// <param name="log">The base actor logger.</param>
    /// <param name="store">The Sable store used to open a session per operation.</param>
    /// <param name="services">Resolves operation-scoped page dependencies.</param>
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
        var contentValidator = _services.GetRequiredService<IHtmlContentValidator>();
        var styleCompiler = _services.GetRequiredService<IStyleCompiler>();
        var styleProfileResolver = _services.GetRequiredService<ISiteStyleProfileResolver>();
        var aliasWriter = _services.GetService<IPageRouteAliasWriter>();
        var fixedSiteContext = new FixedSiteContext(siteId);
        var pageTreeService = new PageTreeService(
            session,
            fixedSiteContext,
            bus,
            _services.GetRequiredService<ILogger<PageTreeService>>(),
            aliasWriter);
        return new AeroPageContentService(
            session,
            bus,
            fixedSiteContext,
            logger,
            contentValidator,
            styleCompiler,
            styleProfileResolver,
            "system",
            cache,
            pageTreeService,
            aliasWriter);
    }

    // ── IHaveState<PageViewModel> ────────────────────────────────────

    /// <inheritdoc />
public Task<PageViewModel> GetStateAsync(CancellationToken ct)
        => Task.FromResult(_state);

    /// <inheritdoc />
public Task UpdateStateAsync(PageViewModel state, CancellationToken ct)
    {
        _state = state;
        return Task.CompletedTask;
    }

    // ── ICruddable<PageViewModel, long> ──────────────────────────────

    /// <inheritdoc />
    public Task<AeroRequestResponse<PageViewModel>> GetByIdAsync(long id, CancellationToken ct)
        => Task.FromResult(Fail("An explicit site scope is required."));

    /// <inheritdoc />
    public async Task<AeroRequestResponse<PageViewModel>> GetByIdAsync(long id, long siteId, CancellationToken ct)
    {
        await using var session = await _store.LightweightSessionAsync();
        var pageService = CreatePageService(session, siteId);
        var result = await pageService.LoadAsync(id, ct);

        return result switch
        {
            Result<PageDocument?, AeroError>.Ok { Value: not null } ok => Ok(ok.Value.ToViewModel()),
            _ => NotFound($"Page {id} not found")
        };
    }

    /// <inheritdoc />
    public Task<AeroRequestResponse<PageViewModel>> GetByIdsAsync(long[] ids, CancellationToken ct)
        => Task.FromResult(Fail("An explicit site scope is required."));

    /// <inheritdoc />
public async Task<AeroRequestResponse<PageViewModel>> CreateAsync(IRequest request, CancellationToken ct)
    {
        if (request is not CreatePageRequest create)
            return Fail("Expected CreatePageRequest");

        await using var session = await _store.LightweightSessionAsync();
        var pageService = CreatePageService(session, create.SiteId);
        var result = await pageService.CreateAsync(create, ct);

        if (result is Result<PageDocument, AeroError>.Ok ok)
            return Ok(ok.Value.ToViewModel());
        if (result is Result<PageDocument, AeroError>.Failure fail)
            return Fail(fail.Error.ToString() ?? "Create failed");
        return Fail("Unexpected result");
    }

    /// <inheritdoc />
public Task<AeroRequestResponse<PageViewModel>> UpdateAsync(IRequest request, CancellationToken ct)
        => Task.FromResult(Fail("An explicit site scope is required."));

    /// <inheritdoc />
    public async Task<AeroRequestResponse<PageViewModel>> UpdateAsync(
        IRequest request,
        long siteId,
        CancellationToken ct)
    {
        if (request is not UpdatePageRequest update)
            return Fail("Expected UpdatePageRequest");

        await using var session = await _store.LightweightSessionAsync();
        var pageService = CreatePageService(session, siteId);
        var result = await pageService.UpdateAsync(update.Id, update, ct);

        return result switch
        {
            Result<PageDocument, AeroError>.Ok ok => Ok(ok.Value.ToViewModel()),
            Result<PageDocument, AeroError>.Failure { Error: AeroError.NotFound } =>
                NotFound($"Page {update.Id} not found"),
            Result<PageDocument, AeroError>.Failure fail =>
                Fail(fail.Error.ToString() ?? "Update failed"),
            _ => Fail("Unexpected result")
        };
    }

    /// <inheritdoc />
public Task<AeroRequestResponse<PageViewModel>> DeleteAsync(IRequest request, CancellationToken ct)
        => Task.FromResult(Fail("An explicit site scope is required."));

    /// <inheritdoc />
    public async Task<AeroRequestResponse<PageViewModel>> DeleteAsync(
        IRequest request,
        long siteId,
        CancellationToken ct)
    {
        if (request is not DeletePageRequest delete)
            return Fail("Expected DeletePageRequest");

        var lookup = await GetByIdAsync(delete.Id, siteId, ct);
        if (!string.IsNullOrWhiteSpace(lookup.error.Message))
            return lookup;

        await using var session = await _store.LightweightSessionAsync();
        var pageService = CreatePageService(session, siteId);
        var result = await pageService.DeleteAsync(delete.Id, ct);

        return result switch
        {
            Result<bool, AeroError>.Ok => Ok(lookup.data),
            Result<bool, AeroError>.Failure { Error: AeroError.NotFound } =>
                NotFound($"Page {delete.Id} not found"),
            Result<bool, AeroError>.Failure fail =>
                Fail(fail.Error.ToString() ?? "Delete failed"),
            _ => Fail("Unexpected result")
        };
    }

    // ── ICanFindBySite<PageViewModel, long> ──────────────────────────

    /// <inheritdoc />
    /// <remarks>
    /// Although the contract is singular, this implementation performs a paged query
    /// and returns only the first item from that page.
    /// </remarks>
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

    /// <inheritdoc />
public Task<AeroRequestResponse<PageViewModel>> GetBySlugAsync(long siteId, string slug, CancellationToken ct)
        => GetBySlugCoreAsync(siteId, slug, culture: null, ct);

    /// <inheritdoc />
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

    /// <inheritdoc />
    public async Task<PageRouteChangeImpact> GetRouteChangeImpactAsync(
        long id,
        long siteId,
        string slug,
        long? parentId,
        CancellationToken ct)
    {
        var lookup = await GetByIdAsync(id, siteId, ct);
        if (!string.IsNullOrWhiteSpace(lookup.error.Message))
            return new PageRouteChangeImpact(id, string.Empty, string.Empty, [], $"Page {id} not found.");

        await using var session = await _store.LightweightSessionAsync();
        var siteContext = new FixedSiteContext(siteId);
        var treeService = new PageTreeService(
            session,
            siteContext,
            _services.GetRequiredService<IMessageBus>(),
            _services.GetRequiredService<ILogger<PageTreeService>>(),
            _services.GetService<IPageRouteAliasWriter>());

        var result = await treeService.GetRouteChangeImpactAsync(id, parentId, slug, ct);
        return result switch
        {
            Result<PageRouteChangeImpact, AeroError>.Ok ok => ok.Value,
            Result<PageRouteChangeImpact, AeroError>.Failure failure =>
                new PageRouteChangeImpact(
                    id,
                    lookup.data.Path ?? string.Empty,
                    lookup.data.Path ?? string.Empty,
                    [],
                    failure.Error.ToString()),
            _ => new PageRouteChangeImpact(
                id,
                lookup.data.Path ?? string.Empty,
                lookup.data.Path ?? string.Empty,
                [],
                "Unexpected route-impact result.")
        };
    }

    /// <inheritdoc />
    /// <remarks>Service failures are represented as an empty page and a zero count.</remarks>
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

    /// <inheritdoc />
    public async Task<AeroRequestResponse<PageViewModel>> PublishAsync(
        long id,
        long siteId,
        CancellationToken ct)
    {
        await using var scope = _services.CreateAsyncScope();
        var workflow = scope.ServiceProvider.GetRequiredService<IPagePublishingWorkflowService>();
        var result = await workflow.PublishNowAsync(id, siteId, ct);
        if (result is Result<bool, AeroError>.Failure { Error: AeroError.NotFound })
            return NotFound($"Page {id} not found");
        if (result is Result<bool, AeroError>.Failure fail)
            return Fail(fail.Error.ToString() ?? "Publish failed");

        return await GetByIdAsync(id, siteId, ct);
    }

    /// <inheritdoc />
    public Task<AeroRequestResponse<PageViewModel>> UnpublishAsync(
        long id,
        long siteId,
        CancellationToken ct)
        => TogglePublishStateAsync(id, siteId, ContentPublicationState.Draft, ct);

    /// <inheritdoc />
    public async Task<List<PageViewModel>> ListCultureVariantsAsync(
        long id,
        long siteId,
        CancellationToken ct)
    {
        await using var session = await _store.LightweightSessionAsync();
        var pageService = CreatePageService(session, siteId);
        var sourceResult = await pageService.LoadAsync(id, ct);
        if (sourceResult is not Result<PageDocument?, AeroError>.Ok { Value: not null } source)
            return [];

        var translationGroupId = source.Value.TranslationGroupId ?? source.Value.Id;
        var result = await pageService.ListCultureVariantsAsync(translationGroupId, ct);
        return result is Result<IReadOnlyList<PageDocument>, AeroError>.Ok ok
            ? ok.Value.Select(p => p.ToViewModel()).ToList()
            : [];
    }

    /// <inheritdoc />
    public async Task<AeroRequestResponse<PageViewModel>> ForkPageForCultureAsync(
        long id,
        long siteId,
        string culture,
        string slug,
        CancellationToken ct)
    {
        await using var session = await _store.LightweightSessionAsync();
        var pageService = CreatePageService(session, siteId);
        var result = await pageService.ForkPageForCultureAsync(id, culture, slug, ct);

        return result switch
        {
            Result<PageDocument, AeroError>.Ok ok => Ok(ok.Value.ToViewModel()),
            Result<PageDocument, AeroError>.Failure { Error: AeroError.NotFound } =>
                NotFound($"Page {id} not found"),
            Result<PageDocument, AeroError>.Failure fail =>
                Fail(fail.Error.ToString() ?? "Culture fork failed"),
            _ => Fail("Unexpected result")
        };
    }

    private async Task<AeroRequestResponse<PageViewModel>> TogglePublishStateAsync(
        long id,
        long siteId,
        ContentPublicationState state,
        CancellationToken ct)
    {
        await using var session = await _store.LightweightSessionAsync();
        var pageService = CreatePageService(session, siteId);
        var lookup = await pageService.LoadAsync(id, ct);
        if (lookup is not Result<PageDocument?, AeroError>.Ok { Value: not null } pageResult)
            return NotFound($"Page {id} not found");

        var page = pageResult.Value;
        var now = DateTimeOffset.UtcNow;
        if (state == ContentPublicationState.Published)
        {
            page.PublishDraftContent(now);
        }
        else
        {
            page.PublicationState = state;
            page.PublishedOn = null;
            page.ModifiedOn = now;
        }

        session.Store(page);
        await session.SaveChangesAsync(ct);
        return Ok(page.ToViewModel());
    }

    /// <inheritdoc />
    public async Task<PageBulkDeleteActorResult> DeleteMultipleAsync(
        long[] ids,
        long siteId,
        bool deleteDescendants,
        CancellationToken ct)
    {
        if (ids.Length == 0)
            return new PageBulkDeleteActorResult();

        await using var session = await _store.LightweightSessionAsync();
        var pageService = CreatePageService(session, siteId);
        var result = await pageService.DeleteMultipleAsync(ids, deleteDescendants, ct);
        return result switch
        {
            Result<int, AeroError>.Ok ok => new PageBulkDeleteActorResult { Deleted = ok.Value },
            Result<int, AeroError>.Failure { Error: AeroError.NotFound } =>
                new PageBulkDeleteActorResult { NotFound = true },
            Result<int, AeroError>.Failure failure =>
                new PageBulkDeleteActorResult { Error = failure.Error.ToString() },
            _ => new PageBulkDeleteActorResult { Error = "Unexpected bulk delete result." }
        };
    }

    // ── AeroRequestResponse helpers ────────────────────────────────────

    private static AeroRequestResponse<PageViewModel> Ok(PageViewModel vm)
        => new(vm, new PageErrorViewModel());

    private static AeroRequestResponse<PageViewModel> NotFound(string msg)
        => new(new PageViewModel(), new PageErrorViewModel { Message = msg });

    private static AeroRequestResponse<PageViewModel> Fail(string msg)
        => new(new PageViewModel(), new PageErrorViewModel { Message = msg });

    // ── FixedSiteContext ─────────────────────────────────────────────

    private sealed class FixedSiteContext(long siteId) : ISiteContext
    {
        /// <summary>Gets the operation's fixed site identifier.</summary>
public long SiteId { get; } = siteId;
        /// <summary>Gets the site identifier reused as the tenant identifier.</summary>
public long TenantId { get; } = siteId;
    }
}
