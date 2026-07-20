using Aero.Actors;
using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Abstractions.Requests;
using Aero.Core.Http;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;
using ZiggyCreatures.Caching.Fusion;
using IRequest = Aero.Core.Commands.IRequest;

namespace Aero.Cms.Modules.Docs.Grains;

/// <summary>
/// Adapts the documentation actor contracts to operation-scoped document sessions and services.
/// </summary>
/// <remarks>
/// The grain keeps only an in-memory <see cref="DocViewModel"/> state value; page data is persisted
/// through <see cref="IDocumentStore"/>. Several identifier-based operations resolve a page's site
/// directly from storage and perform no independent authorization check. Callers must authorize
/// identifiers before crossing the actor boundary. Service failures are sometimes collapsed to an
/// empty list by list-returning actor methods.
/// </remarks>
public sealed class AeroDocsGrain : AeroActor, IAeroDocsActor
{
    private readonly IDocumentStore _store;
    private readonly IServiceProvider _services;
    private DocViewModel _state = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="AeroDocsGrain"/> class.
    /// </summary>
    /// <param name="log">The actor logger passed to the base actor.</param>
    /// <param name="store">The store used to open operation-scoped sessions.</param>
    /// <param name="services">The provider used to resolve bus, logger, and optional cache dependencies.</param>
public AeroDocsGrain(
        ILogger<AeroActor> log,
        IDocumentStore store,
        IServiceProvider services)
        : base(log)
    {
        _store = store;
        _services = services;
    }

    // ── Helper: manual construction of DocsContentService ───────────────

    /// <summary>
    /// Creates a content service fixed to the supplied site and the <c>system</c> audit actor.
    /// </summary>
    private DocsContentService CreateDocsService(IDocumentSession session, long siteId)
    {
        var bus = _services.GetRequiredService<IMessageBus>();
        var logger = _services.GetRequiredService<ILogger<DocsContentService>>();
        var cache = _services.GetService<IFusionCache>();
        return new DocsContentService(
            session,
            bus,
            new FixedSiteContext(siteId),
            logger,
            actor: "system",
            cache);
    }

    /// <summary>
    /// Creates a hierarchy service that shares the operation's document session.
    /// </summary>
    private DocsTreeService CreateDocsTreeService(IDocumentSession session)
    {
        var bus = _services.GetRequiredService<IMessageBus>();
        var logger = _services.GetRequiredService<ILogger<DocsTreeService>>();
        return new DocsTreeService(session, bus, logger);
    }

    // ── IHaveState<DocViewModel> ────────────────────────────────────────

/// <inheritdoc />
public Task<DocViewModel> GetStateAsync(CancellationToken ct)
        => Task.FromResult(_state);

/// <inheritdoc />
public Task UpdateStateAsync(DocViewModel state, CancellationToken ct)
    {
        _state = state;
        return Task.CompletedTask;
    }

    // ── ICruddable<DocViewModel, long> ──────────────────────────────────

    /// <summary>
    /// Loads a page by identifier and returns its view model.
    /// </summary>
    /// <param name="id">The page identifier.</param>
    /// <param name="ct">The token used for the query session.</param>
    /// <returns>The page response, or a not-found response.</returns>
    /// <remarks>
    /// This actor contract provides no site identifier, so the load is not site-scoped and no
    /// authorization check is performed by the grain.
    /// </remarks>
    public async Task<AeroRequestResponse<DocViewModel>> GetByIdAsync(long id, CancellationToken ct)
    {
        await using var session = await _store.QuerySessionAsync();
        var doc = await session.LoadAsync<DocsPage>(id, ct);

        return doc is not null
            ? Ok(doc.ToViewModel())
            : NotFound($"Doc {id} not found");
    }

    /// <summary>
    /// Loads a set of identifiers and returns only the first matching view model.
    /// </summary>
    /// <param name="ids">The identifiers to query without a site filter.</param>
    /// <param name="ct">The token used for the lightweight session.</param>
    /// <returns>The first match, an empty model when no records match, or an error response.</returns>
    /// <remarks>Callers must authorize every identifier before invoking this cross-site query.</remarks>
public async Task<AeroRequestResponse<DocViewModel>> GetByIdsAsync(long[] ids, CancellationToken ct)
    {
        await using var session = await _store.LightweightSessionAsync();
        // ICruddable doesn't provide siteId; the service queries by IDs
        // without scoping to a site (caller is responsible for auth).
        var docsService = CreateDocsService(session, siteId: 0);
        var result = await docsService.GetByIdsAsync(ids, ct);

        if (result is Result<IReadOnlyList<DocsPage>, AeroError>.Ok ok)
        {
            var primary = ok.Value.Count > 0 ? ok.Value[0].ToViewModel() : new DocViewModel();
            return Ok(primary);
        }
        if (result is Result<IReadOnlyList<DocsPage>, AeroError>.Failure fail)
            return Fail(fail.Error.ToString() ?? "GetByIds failed");
        return Ok(new DocViewModel());
    }

/// <inheritdoc />
public async Task<AeroRequestResponse<DocViewModel>> CreateAsync(IRequest request, CancellationToken ct)
    {
        if (request is not CreateDocRequest create)
            return Fail("Expected CreateDocRequest");

        await using var session = await _store.LightweightSessionAsync();
        var docsService = CreateDocsService(session, create.SiteId);
        var result = await docsService.CreateAsync(create, ct);

        if (result is Result<DocsPage, AeroError>.Ok ok)
            return Ok(ok.Value.ToViewModel());
        if (result is Result<DocsPage, AeroError>.Failure fail)
            return Fail(fail.Error.ToString() ?? "Create failed");
        return Fail("Unexpected result");
    }

/// <inheritdoc />
public async Task<AeroRequestResponse<DocViewModel>> UpdateAsync(IRequest request, CancellationToken ct)
    {
        if (request is not UpdateDocRequest update)
            return Fail("Expected UpdateDocRequest");

        // Load doc to obtain its SiteId (not present on UpdateDocRequest)
        await using var loadSession = await _store.QuerySessionAsync();
        var doc = await loadSession.LoadAsync<DocsPage>(update.Id, ct);

        if (doc is null)
            return NotFound($"Doc {update.Id} not found");

        var siteId = doc.SiteId;

        await using var session = await _store.LightweightSessionAsync();
        var docsService = CreateDocsService(session, siteId);
        var result = await docsService.UpdateAsync(update.Id, update, ct);

        if (result is Result<DocsPage, AeroError>.Ok ok)
            return Ok(ok.Value.ToViewModel());
        if (result is Result<DocsPage, AeroError>.Failure fail)
            return Fail(fail.Error.ToString() ?? "Update failed");
        return Fail("Unexpected result");
    }

/// <inheritdoc />
public async Task<AeroRequestResponse<DocViewModel>> DeleteAsync(IRequest request, CancellationToken ct)
    {
        if (request is not DeleteDocRequest delete)
            return Fail("Expected DeleteDocRequest");

        // Load doc to obtain SiteId and capture the view model
        await using var loadSession = await _store.QuerySessionAsync();
        var doc = await loadSession.LoadAsync<DocsPage>(delete.Id, ct);

        if (doc is null)
            return NotFound($"Doc {delete.Id} not found");

        var siteId = doc.SiteId;
        var vm = doc.ToViewModel();

        await using var session = await _store.LightweightSessionAsync();
        var docsService = CreateDocsService(session, siteId);
        var result = await docsService.DeleteAsync(delete.Id, ct);

        if (result is Result<bool, AeroError>.Ok)
            return Ok(vm);
        if (result is Result<bool, AeroError>.Failure fail)
            return Fail(fail.Error.ToString() ?? "Delete failed");
        return Fail("Unexpected result");
    }

    // ── ICanFindBySite<DocViewModel, long> ──────────────────────────────

/// <inheritdoc />
/// <remarks>
/// Paging is applied in memory after loading all site pages, and only the first model from the
/// selected page is returned. An empty page produces an empty model.
/// </remarks>
public async Task<AeroRequestResponse<DocViewModel>> GetBySiteIdAsync(
        long siteId, int page = 1, int rows = 10, CancellationToken ct = default)
    {
        await using var session = await _store.LightweightSessionAsync();
        var docsService = CreateDocsService(session, siteId);
        var result = await docsService.GetAllAsync(ct);

        if (result is Result<IReadOnlyList<DocsPage>, AeroError>.Ok ok)
        {
            var paged = ok.Value.Skip((page - 1) * rows).Take(rows).ToList();
            return paged.Count > 0
                ? Ok(paged[0].ToViewModel())
                : Ok(new DocViewModel());
        }
        if (result is Result<IReadOnlyList<DocsPage>, AeroError>.Failure fail)
            return Fail(fail.Error.ToString() ?? "GetBySiteId failed");
        return Ok(new DocViewModel());
    }

    // ── ICanFindBySlug ──────────────────────────────────────────────────

/// <inheritdoc />
public Task<AeroRequestResponse<DocViewModel>> GetBySlugAsync(long siteId, string slug, CancellationToken ct)
        => GetBySlugCoreAsync(siteId, slug, ct);

    /// <inheritdoc />
    Task<AeroRequestResponse<DocViewModel>> ICanFindBySlug<DocViewModel, string>.GetBySlugAsync(
        string siteId, string slug, CancellationToken ct)
    {
        if (long.TryParse(siteId, out var id))
            return GetBySlugCoreAsync(id, slug, ct);
        return Task.FromResult(Fail($"Invalid site ID: {siteId}"));
    }

    /// <summary>
    /// Performs the site-scoped slug lookup shared by the two actor contract shapes.
    /// </summary>
    private async Task<AeroRequestResponse<DocViewModel>> GetBySlugCoreAsync(
        long siteId, string slug, CancellationToken ct)
    {
        await using var session = await _store.LightweightSessionAsync();
        var docsService = CreateDocsService(session, siteId);
        var result = await docsService.GetBySlugAsync(slug, ct);

        if (result is Result<DocsPage?, AeroError>.Ok { Value: not null } ok)
            return Ok(ok.Value.ToViewModel());
        if (result is Result<DocsPage?, AeroError>.Ok)
            return NotFound($"Doc with slug '{slug}' not found");
        if (result is Result<DocsPage?, AeroError>.Failure fail)
            return Fail(fail.Error.ToString() ?? $"Doc with slug '{slug}' not found");
        return Fail("Unexpected result");
    }

    // ── IAeroDocsActor doc-specific methods ───────────────────────────────

/// <inheritdoc />
/// <remarks>Service failures are represented as an empty list.</remarks>
public async Task<List<DocViewModel>> GetAllBySiteAsync(long siteId, CancellationToken ct = default)
    {
        await using var session = await _store.LightweightSessionAsync();
        var docsService = CreateDocsService(session, siteId);
        var result = await docsService.GetAllAsync(ct);

        if (result is Result<IReadOnlyList<DocsPage>, AeroError>.Ok ok)
            return ok.Value.Select(d => d.ToViewModel()).ToList();
        return [];
    }

/// <inheritdoc />
/// <remarks>
/// The lookup is site-scoped and uses the actor execution context's current UI culture rather than
/// an explicit culture parameter. Publication state is not filtered, and failures become an empty list.
/// </remarks>
public async Task<List<DocViewModel>> GetChildrenAsync(long parentId, long siteId, CancellationToken ct = default)
    {
        await using var session = await _store.LightweightSessionAsync();
        var docsService = CreateDocsService(session, siteId);
        var result = await docsService.GetChildrenAsync(parentId, ct);

        if (result is Result<IReadOnlyList<DocsPage>, AeroError>.Ok ok)
            return ok.Value.Select(d => d.ToViewModel()).ToList();
        return [];
    }

/// <inheritdoc />
/// <remarks>The lookup is not culture- or publication-scoped; failures become an empty list.</remarks>
public async Task<List<DocViewModel>> GetTopLevelCategoriesAsync(long siteId, CancellationToken ct = default)
    {
        await using var session = await _store.LightweightSessionAsync();
        var docsService = CreateDocsService(session, siteId);
        var result = await docsService.GetTopLevelCategoriesAsync(ct);

        if (result is Result<IReadOnlyList<DocsPage>, AeroError>.Ok ok)
            return ok.Value.Select(d => d.ToViewModel()).ToList();
        return [];
    }

/// <inheritdoc />
public async Task<AeroRequestResponse<DocViewModel>> SaveAsync(DocViewModel vm, CancellationToken ct = default)
    {
        await using var session = await _store.LightweightSessionAsync();
        var docsService = CreateDocsService(session, vm.SiteId);
        var result = await docsService.SaveFromViewModelAsync(vm, ct);

        if (result is Result<DocsPage, AeroError>.Ok ok)
            return Ok(ok.Value.ToViewModel());
        if (result is Result<DocsPage, AeroError>.Failure fail)
            return Fail(fail.Error.ToString() ?? "Save failed");
        return Fail("Unexpected result");
    }

/// <inheritdoc />
/// <remarks>
/// The source identifier is loaded without an expected-site constraint. A missing record or
/// service failure is represented as an empty list.
/// </remarks>
public async Task<List<DocViewModel>> ListCultureVariantsAsync(long id, CancellationToken ct = default)
    {
        var siteIdResult = await ResolveSiteIdAsync(id, ct);
        if (siteIdResult is null)
            return [];

        await using var session = await _store.LightweightSessionAsync();
        var docsService = CreateDocsService(session, siteIdResult.Value);
        var result = await docsService.ListCultureVariantsAsync(id, ct);

        if (result is Result<IReadOnlyList<DocsPage>, AeroError>.Ok ok)
            return ok.Value.Select(d => d.ToViewModel()).ToList();
        return [];
    }

/// <inheritdoc />
public async Task<AeroRequestResponse<DocViewModel>> ForkDocForCultureAsync(long id, string culture, string slug, CancellationToken ct = default)
    {
        var siteIdResult = await ResolveSiteIdAsync(id, ct);
        if (siteIdResult is null)
            return NotFound($"Doc {id} not found");

        await using var session = await _store.LightweightSessionAsync();
        var docsService = CreateDocsService(session, siteIdResult.Value);
        var result = await docsService.ForkToCultureAsync(id, culture, slug, ct);

        if (result is Result<DocsPage, AeroError>.Ok ok)
            return Ok(ok.Value.ToViewModel());
        if (result is Result<DocsPage, AeroError>.Failure fail)
            return Fail(fail.Error.ToString() ?? "Fork doc translation failed");
        return Fail("Unexpected result");
    }

/// <inheritdoc />
public async Task<AeroRequestResponse<DocViewModel>> PublishAsync(long id, CancellationToken ct = default)
    {
        var siteIdResult = await ResolveSiteIdAsync(id, ct);
        if (siteIdResult is null)
            return NotFound($"Doc {id} not found");

        await using var session = await _store.LightweightSessionAsync();
        var docsService = CreateDocsService(session, siteIdResult.Value);
        var result = await docsService.PublishAsync(id, ct);

        if (result is Result<DocsPage, AeroError>.Ok ok)
            return Ok(ok.Value.ToViewModel());
        if (result is Result<DocsPage, AeroError>.Failure fail)
            return Fail(fail.Error.ToString() ?? "Publish failed");
        return Fail("Unexpected result");
    }

/// <inheritdoc />
public async Task<AeroRequestResponse<DocViewModel>> UnpublishAsync(long id, CancellationToken ct = default)
    {
        var siteIdResult = await ResolveSiteIdAsync(id, ct);
        if (siteIdResult is null)
            return NotFound($"Doc {id} not found");

        await using var session = await _store.LightweightSessionAsync();
        var docsService = CreateDocsService(session, siteIdResult.Value);
        var result = await docsService.UnpublishAsync(id, ct);

        if (result is Result<DocsPage, AeroError>.Ok ok)
            return Ok(ok.Value.ToViewModel());
        if (result is Result<DocsPage, AeroError>.Failure fail)
            return Fail(fail.Error.ToString() ?? "Unpublish failed");
        return Fail("Unexpected result");
    }

/// <inheritdoc />
public async Task<AeroRequestResponse<DocViewModel>> CreateChildSectionAsync(
        long siteId,
        long spaceId,
        long parentId,
        string title,
        string? summary,
        CancellationToken ct = default)
    {
        await using var session = await _store.LightweightSessionAsync();
        var treeService = CreateDocsTreeService(session);
        var result = await treeService.CreateChildSectionAsync(siteId, spaceId, parentId, title, summary, ct);

        if (result is Result<DocsPage, AeroError>.Ok ok)
            return Ok(ok.Value.ToViewModel());
        if (result is Result<DocsPage, AeroError>.Failure fail)
            return Fail(fail.Error.ToString() ?? "Create child section failed");
        return Fail("Unexpected result");
    }

/// <inheritdoc />
public async Task<AeroRequestResponse<DocViewModel>> MoveSectionAsync(
        long siteId,
        long spaceId,
        long sectionId,
        long newParentId,
        int? order,
        bool rewriteSlug,
        CancellationToken ct = default)
    {
        await using var session = await _store.LightweightSessionAsync();
        var treeService = CreateDocsTreeService(session);
        var result = await treeService.MoveSectionAsync(siteId, spaceId, sectionId, newParentId, order, rewriteSlug, ct);

        if (result is Result<DocsPage, AeroError>.Ok ok)
            return Ok(ok.Value.ToViewModel());
        if (result is Result<DocsPage, AeroError>.Failure fail)
            return Fail(fail.Error.ToString() ?? "Move section failed");
        return Fail("Unexpected result");
    }

/// <inheritdoc />
public async Task<AeroRequestResponse<DocViewModel>> ReorderSectionsAsync(
        long siteId,
        long spaceId,
        long parentId,
        IReadOnlyList<long> orderedIds,
        CancellationToken ct = default)
    {
        await using var session = await _store.LightweightSessionAsync();
        var treeService = CreateDocsTreeService(session);
        var result = await treeService.ReorderSiblingsAsync(siteId, spaceId, parentId, orderedIds, ct);

        if (result is Result<bool, AeroError>.Ok)
            return Ok(new DocViewModel());
        if (result is Result<bool, AeroError>.Failure fail)
            return Fail(fail.Error.ToString() ?? "Reorder sections failed");
        return Fail("Unexpected result");
    }

    /// <summary>
    /// Loads only the site identifier associated with a page identifier.
    /// </summary>
    /// <remarks>The identifier lookup is not authorized or constrained by an expected site.</remarks>
    private async Task<long?> ResolveSiteIdAsync(long id, CancellationToken ct)
    {
        await using var loadSession = await _store.QuerySessionAsync();
        var doc = await loadSession.LoadAsync<DocsPage>(id, ct);
        return doc?.SiteId;
    }

    // ── AeroRequestResponse helpers ──────────────────────────────────────

    /// <summary>
    /// Creates a successful actor response with an empty error model.
    /// </summary>
    private static AeroRequestResponse<DocViewModel> Ok(DocViewModel vm)
        => new(vm, new DocErrorViewModel());

    /// <summary>
    /// Creates an error response with an empty data model.
    /// </summary>
    private static AeroRequestResponse<DocViewModel> NotFound(string msg)
        => new(new DocViewModel(), new DocErrorViewModel { Message = msg });

    /// <summary>
    /// Creates an operation-failure response with an empty data model.
    /// </summary>
    private static AeroRequestResponse<DocViewModel> Fail(string msg)
        => new(new DocViewModel(), new DocErrorViewModel { Message = msg });

    // ── FixedSiteContext ─────────────────────────────────────────────────

    /// <summary>
    /// Supplies an explicit site boundary to manually constructed content services.
    /// </summary>
    /// <remarks>The current implementation uses the site identifier as the tenant identifier.</remarks>
    private sealed class FixedSiteContext(long siteId) : ISiteContext
    {
        /// <summary>
        /// Gets the fixed site identifier.
        /// </summary>
public long SiteId { get; } = siteId;

        /// <summary>
        /// Gets the tenant identifier, which is equal to <see cref="SiteId"/>.
        /// </summary>
public long TenantId { get; } = siteId;
    }
}
