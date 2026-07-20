using System.Globalization;
using System.Text.Json;
using Aero.Actors;
using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Content.Serialization;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Events;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Modules.Content.Events;
using Microsoft.Extensions.DependencyInjection;
using IRequest = Aero.Core.Commands.IRequest;

namespace Aero.Cms.Modules.Content.Grains;

/// <summary>
/// Orleans grain for content item management — wraps existing
/// <see cref="IContentService"/> / <see cref="ContentCommandService"/> via
/// <see cref="IServiceScopeFactory"/> to handle complex entity logic.
/// </summary>
/// <remarks>
/// Persistence operations accept site identity from their arguments or view models; the grain does
/// not independently authorize callers or compare against a current-site context. Identifier-only
/// delete and publication operations therefore require callers to enforce site ownership.
/// Notifications are published after successful persistence and are best effort.
/// </remarks>
public sealed class AeroContentItemGrain : AeroActor, IAeroContentItemActor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private ContentItemViewModel _state = new();

        /// <summary>
    /// Initializes the grain with its actor logger and service-scope factory.
    /// </summary>
    /// <param name="log">The logger forwarded to the actor base.</param>
    /// <param name="scopeFactory">The factory used to isolate scoped services per operation.</param>
public AeroContentItemGrain(
        ILogger<AeroActor> log,
        IServiceScopeFactory scopeFactory)
        : base(log)
    {
        _scopeFactory = scopeFactory;
    }

    // ── IHaveState<ContentItemViewModel> ─────────────────────────────

        /// <summary>
    /// Returns the grain's in-memory view-model state.
    /// </summary>
    /// <param name="ct">Ignored because no asynchronous or persistent work occurs.</param>
    /// <returns>The current mutable state instance.</returns>
    /// <remarks>The state is not persisted by this grain and is returned without cloning.</remarks>
public Task<ContentItemViewModel> GetStateAsync(CancellationToken ct)
        => Task.FromResult(_state);

        /// <summary>
    /// Replaces the grain's in-memory view-model state.
    /// </summary>
    /// <param name="state">The mutable instance retained by reference.</param>
    /// <param name="ct">Ignored because no asynchronous or persistent work occurs.</param>
    /// <returns>A task already completed after assignment.</returns>
public Task UpdateStateAsync(ContentItemViewModel state, CancellationToken ct)
    {
        _state = state;
        return Task.CompletedTask;
    }

    // ── ICruddable<ContentItemViewModel, long> ───────────────────────

        /// <summary>
    /// Loads an item by its globally unique identifier and maps service failures to not found.
    /// </summary>
    /// <remarks>No site predicate or ownership check is applied.</remarks>
public async Task<AeroRequestResponse<ContentItemViewModel>> GetByIdAsync(long id, CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var contentService = scope.ServiceProvider.GetRequiredService<IContentService>();

        var result = await contentService.LoadAsync(id, ct);
        return result switch
        {
            Result<ContentItem, AeroError>.Ok ok => Ok(MapToViewModel(ok.Value)),
            _ => NotFound($"Content item {id} not found")
        };
    }

        /// <summary>
    /// Reports that batch identifier lookup is unsupported.
    /// </summary>
    /// <returns>A completed response whose error message describes the unsupported operation.</returns>
public Task<AeroRequestResponse<ContentItemViewModel>> GetByIdsAsync(long[] ids, CancellationToken ct)
        => Task.FromResult(Fail("GetByIdsAsync not supported for content items"));

        /// <summary>
    /// Reports that request-based creation is unsupported.
    /// </summary>
    /// <remarks>The request and cancellation token are ignored; use <see cref="SaveDraftAsync"/>.</remarks>
public async Task<AeroRequestResponse<ContentItemViewModel>> CreateAsync(IRequest request, CancellationToken ct)
    {
        // Content items don't use the standard IRequest pattern — Create is handled
        // via SaveDraftAsync with a fully constructed ContentItem.
        return Fail("Use CreateContentItemAsync instead of CreateAsync for content items");
    }

        /// <summary>
    /// Reports that request-based updates are unsupported.
    /// </summary>
public Task<AeroRequestResponse<ContentItemViewModel>> UpdateAsync(IRequest request, CancellationToken ct)
        => Task.FromResult(Fail("Use SaveDraftAsync directly for content items"));

        /// <summary>
    /// Reports that request-based deletion is unsupported.
    /// </summary>
public async Task<AeroRequestResponse<ContentItemViewModel>> DeleteAsync(IRequest request, CancellationToken ct)
    {
        // Content items use DeleteAsync(long id) instead of the IRequest pattern.
        // This overload exists to satisfy ICruddable<ContentItemViewModel, long>.
        return Fail("Use DeleteAsync(long id) for content items");
    }

        /// <summary>
    /// Deletes an item by identifier and publishes a post-commit notification when its prior state loaded.
    /// </summary>
    /// <returns>An empty successful view model after deletion, or an error response on failure.</returns>
    /// <remarks>
    /// No site ownership check is applied. A delete may succeed without a notification when the
    /// pre-delete load failed.
    /// </remarks>
public async Task<AeroRequestResponse<ContentItemViewModel>> DeleteAsync(long id, CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var commandService = scope.ServiceProvider.GetRequiredService<ContentCommandService>();
        var contentService = scope.ServiceProvider.GetRequiredService<IContentService>();

        var existing = await contentService.LoadAsync(id, ct);

        var result = await commandService.DeleteAsync(id, ct);
        if (result is Result<bool, AeroError>.Ok)
        {
            if (existing is Result<ContentItem, AeroError>.Ok ok)
            {
                var events = scope.ServiceProvider.GetRequiredService<ContentEventPublisher>();
                await events.PublishBestEffortAsync(
                    new ContentItemViewModelDeleted(MapToViewModel(ok.Value)));
            }
            return Ok(new ContentItemViewModel());
        }

        return Fail("Delete failed");
    }

    // ── ICanFindBySite / ICanFindBySlug — stubbed ────────────────────

        /// <summary>
    /// Reports that site-wide item listing is unsupported by this actor contract.
    /// </summary>
public Task<AeroRequestResponse<ContentItemViewModel>> GetBySiteIdAsync(long siteId, int page = 1, int rows = 10, CancellationToken ct = default)
        => Task.FromResult(Fail("Not supported"));

        /// <summary>
    /// Reports that numeric-site slug lookup is unsupported by this actor contract.
    /// </summary>
public Task<AeroRequestResponse<ContentItemViewModel>> GetBySlugAsync(long siteId, string slug, CancellationToken ct)
        => Task.FromResult(Fail("Not supported"));

    /// <summary>
    /// Reports that string-site slug lookup is unsupported by this actor contract.
    /// </summary>
    Task<AeroRequestResponse<ContentItemViewModel>> ICanFindBySlug<ContentItemViewModel, string>.GetBySlugAsync(string siteId, string slug, CancellationToken ct)
        => Task.FromResult(Fail("Not supported"));

    // ── IAeroContentItemActor content-specific methods ────────────────

        /// <summary>
    /// Queries a page of items by caller-supplied site and content-type alias.
    /// </summary>
    /// <returns>The mapped items and total count, or an empty zero-count tuple for any railway failure.</returns>
public async Task<(List<ContentItemViewModel> Items, long TotalCount)> GetByTypeAsync(
        long siteId, string contentTypeAlias, int skip, int take, CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var queryService = scope.ServiceProvider.GetRequiredService<IContentQueryService>();

        var result = await queryService.GetByTypeAsync(siteId, contentTypeAlias, skip, take, ct);
        return result switch
        {
            Result<(IReadOnlyList<ContentItem> Items, long TotalCount), AeroError>.Ok ok =>
                (ok.Value.Items.Select(MapToViewModel).ToList(), ok.Value.TotalCount),
            _ => ([], 0)
        };
    }

        /// <summary>
    /// Converts and persists a draft, then publishes a best-effort created or updated notification.
    /// </summary>
    /// <returns>The persisted view model or an error response.</returns>
    /// <remarks>
    /// A blank culture fails before scope creation; an invalid nonblank culture throws during
    /// conversion. Site identity is accepted from <paramref name="vm"/> without authorization.
    /// Newness is determined from the incoming identifier before a Snowflake identifier is assigned.
    /// </remarks>
public async Task<AeroRequestResponse<ContentItemViewModel>> SaveDraftAsync(ContentItemViewModel vm, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(vm.Culture))
        {
            return Fail("Culture is required for content items.");
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var commandService = scope.ServiceProvider.GetRequiredService<ContentCommandService>();

        var isNew = vm.Id == 0;
        var item = ToEntity(vm);
        var result = await commandService.SaveDraftAsync(item, ct);

        if (result is Result<ContentItem, AeroError>.Ok ok)
        {
            var viewModel = MapToViewModel(ok.Value);
            var events = scope.ServiceProvider.GetRequiredService<ContentEventPublisher>();
            if (isNew)
                await events.PublishBestEffortAsync(new ContentItemViewModelCreated(viewModel));
            else
                await events.PublishBestEffortAsync(new ContentItemViewModelUpdated(viewModel));
            return Ok(viewModel);
        }

        return result switch
        {
            Result<ContentItem, AeroError>.Failure failure => Fail(failure.Error.ToString()),
            _ => Fail("Unexpected result")
        };
    }

        /// <summary>
    /// Loads and publishes an item by identifier.
    /// </summary>
    /// <returns>A not-found response for load failure, the published model on success, or a generic failure.</returns>
    /// <remarks>No site ownership check or publication notification is performed here.</remarks>
public async Task<AeroRequestResponse<ContentItemViewModel>> PublishAsync(long id, CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var contentService = scope.ServiceProvider.GetRequiredService<IContentService>();
        var commandService = scope.ServiceProvider.GetRequiredService<ContentCommandService>();

        var loadResult = await contentService.LoadAsync(id, ct);
        if (loadResult is not Result<ContentItem, AeroError>.Ok ok)
            return NotFound($"Content item {id} not found");

        var publishResult = await commandService.PublishAsync(ok.Value, ct);
        return publishResult switch
        {
            Result<ContentItem, AeroError>.Ok pubOk => Ok(MapToViewModel(pubOk.Value)),
            _ => Fail("Publish failed")
        };
    }

        /// <summary>
    /// Loads an item by identifier, marks it draft, clears publication time, and saves it.
    /// </summary>
    /// <remarks>No site ownership check or unpublication notification is performed here.</remarks>
public async Task<AeroRequestResponse<ContentItemViewModel>> UnpublishAsync(long id, CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var contentService = scope.ServiceProvider.GetRequiredService<IContentService>();
        var commandService = scope.ServiceProvider.GetRequiredService<ContentCommandService>();

        var loadResult = await contentService.LoadAsync(id, ct);
        if (loadResult is not Result<ContentItem, AeroError>.Ok ok)
            return NotFound($"Content item {id} not found");

        var item = ok.Value;
        item.PublicationState = ContentPublicationState.Draft;
        item.PublishedOn = null;

        var saveResult = await commandService.SaveDraftAsync(item, ct);
        return saveResult switch
        {
            Result<ContentItem, AeroError>.Ok saveOk => Ok(MapToViewModel(saveOk.Value)),
            _ => Fail("Unpublish failed")
        };
    }

    // ── AeroRequestResponse helpers ────────────────────────────────────

    /// <summary>Creates a response with data and an empty error model.</summary>
    private static AeroRequestResponse<ContentItemViewModel> Ok(ContentItemViewModel vm)
        => new(vm, new ContentItemErrorViewModel());

    /// <summary>Creates an empty-data response carrying a not-found message.</summary>
    private static AeroRequestResponse<ContentItemViewModel> NotFound(string msg)
        => new(new ContentItemViewModel(), new ContentItemErrorViewModel { Message = msg });

    /// <summary>Creates an empty-data response carrying a failure message.</summary>
    private static AeroRequestResponse<ContentItemViewModel> Fail(string msg)
        => new(new ContentItemViewModel(), new ContentItemErrorViewModel { Message = msg });

    // ── Mapping ───────────────────────────────────────────────────────

    /// <summary>Serializes fields and projects a content item into its actor contract.</summary>
    private static ContentItemViewModel MapToViewModel(ContentItem item) => new()
    {
        Id = item.Id,
        SiteId = item.SiteId,
        ContentTypeAlias = item.ContentTypeAlias,
        Slug = item.Slug,
        Title = item.Title,
        TranslationGroupId = item.TranslationGroupId,
        Culture = item.Culture,
        SourceItemId = item.SourceItemId,
        FieldsJson = JsonSerializer.Serialize(
            item.Fields,
            ContentJsonContext.Default.DictionaryStringJsonElement),
        PublicationState = item.PublicationState,
        PublishedOn = item.PublishedOn,
        VersionNumber = item.VersionNumber,
        SchedulePublishUtc = item.SchedulePublishUtc,
        ScheduleUnpublishUtc = item.ScheduleUnpublishUtc,
        CreatedOn = item.CreatedOn,
        ModifiedOn = item.ModifiedOn
    };

    /// <summary>
    /// Deserializes fields, canonicalizes culture, and projects a view model into a mutable entity.
    /// </summary>
    /// <remarks>A Snowflake identifier is assigned when the incoming identifier is zero.</remarks>
    private static ContentItem ToEntity(ContentItemViewModel vm)
    {
        var fields = string.IsNullOrWhiteSpace(vm.FieldsJson) || vm.FieldsJson == "{}"
            ? new Dictionary<string, JsonElement>()
            : JsonSerializer.Deserialize(
                vm.FieldsJson,
                ContentJsonContext.Default.DictionaryStringJsonElement)
              ?? new Dictionary<string, JsonElement>();

        return new ContentItem
        {
            Id = vm.Id != 0 ? vm.Id : Snowflake.NewId(),
            SiteId = vm.SiteId,
            ContentTypeAlias = vm.ContentTypeAlias,
            Title = vm.Title,
            Slug = vm.Slug,
            TranslationGroupId = vm.TranslationGroupId,
            Culture = CultureInfo.GetCultureInfo(vm.Culture).Name,
            SourceItemId = vm.SourceItemId,
            Fields = fields,
            PublicationState = vm.PublicationState,
            PublishedOn = vm.PublishedOn,
            VersionNumber = vm.VersionNumber,
            SchedulePublishUtc = vm.SchedulePublishUtc,
            ScheduleUnpublishUtc = vm.ScheduleUnpublishUtc
        };
    }
}
