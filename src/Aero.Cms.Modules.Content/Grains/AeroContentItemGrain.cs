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
public sealed class AeroContentItemGrain : AeroActor, IAeroContentItemActor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private ContentItemViewModel _state = new();

        /// <summary>
    /// Initializes a new instance of the <see cref="AeroContentItemGrain"/> class.
    /// </summary>
public AeroContentItemGrain(
        ILogger<AeroActor> log,
        IServiceScopeFactory scopeFactory)
        : base(log)
    {
        _scopeFactory = scopeFactory;
    }

    // ── IHaveState<ContentItemViewModel> ─────────────────────────────

        /// <summary>
    /// GetStateAsync method.
    /// </summary>
public Task<ContentItemViewModel> GetStateAsync(CancellationToken ct)
        => Task.FromResult(_state);

        /// <summary>
    /// UpdateStateAsync method.
    /// </summary>
public Task UpdateStateAsync(ContentItemViewModel state, CancellationToken ct)
    {
        _state = state;
        return Task.CompletedTask;
    }

    // ── ICruddable<ContentItemViewModel, long> ───────────────────────

        /// <summary>
    /// GetByIdAsync method.
    /// </summary>
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
    /// GetByIdsAsync method.
    /// </summary>
public Task<AeroRequestResponse<ContentItemViewModel>> GetByIdsAsync(long[] ids, CancellationToken ct)
        => Task.FromResult(Fail("GetByIdsAsync not supported for content items"));

        /// <summary>
    /// CreateAsync method.
    /// </summary>
public async Task<AeroRequestResponse<ContentItemViewModel>> CreateAsync(IRequest request, CancellationToken ct)
    {
        // Content items don't use the standard IRequest pattern — Create is handled
        // via SaveDraftAsync with a fully constructed ContentItem.
        return Fail("Use CreateContentItemAsync instead of CreateAsync for content items");
    }

        /// <summary>
    /// UpdateAsync method.
    /// </summary>
public Task<AeroRequestResponse<ContentItemViewModel>> UpdateAsync(IRequest request, CancellationToken ct)
        => Task.FromResult(Fail("Use SaveDraftAsync directly for content items"));

        /// <summary>
    /// DeleteAsync method.
    /// </summary>
public async Task<AeroRequestResponse<ContentItemViewModel>> DeleteAsync(IRequest request, CancellationToken ct)
    {
        // Content items use DeleteAsync(long id) instead of the IRequest pattern.
        // This overload exists to satisfy ICruddable<ContentItemViewModel, long>.
        return Fail("Use DeleteAsync(long id) for content items");
    }

        /// <summary>
    /// DeleteAsync method.
    /// </summary>
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
    /// GetBySiteIdAsync method.
    /// </summary>
public Task<AeroRequestResponse<ContentItemViewModel>> GetBySiteIdAsync(long siteId, int page = 1, int rows = 10, CancellationToken ct = default)
        => Task.FromResult(Fail("Not supported"));

        /// <summary>
    /// GetBySlugAsync method.
    /// </summary>
public Task<AeroRequestResponse<ContentItemViewModel>> GetBySlugAsync(long siteId, string slug, CancellationToken ct)
        => Task.FromResult(Fail("Not supported"));

    Task<AeroRequestResponse<ContentItemViewModel>> ICanFindBySlug<ContentItemViewModel, string>.GetBySlugAsync(string siteId, string slug, CancellationToken ct)
        => Task.FromResult(Fail("Not supported"));

    // ── IAeroContentItemActor content-specific methods ────────────────

        /// <summary>
    /// GetByTypeAsync method.
    /// </summary>
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
    /// SaveDraftAsync method.
    /// </summary>
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
    /// PublishAsync method.
    /// </summary>
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
    /// UnpublishAsync method.
    /// </summary>
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

    private static AeroRequestResponse<ContentItemViewModel> Ok(ContentItemViewModel vm)
        => new(vm, new ContentItemErrorViewModel());

    private static AeroRequestResponse<ContentItemViewModel> NotFound(string msg)
        => new(new ContentItemViewModel(), new ContentItemErrorViewModel { Message = msg });

    private static AeroRequestResponse<ContentItemViewModel> Fail(string msg)
        => new(new ContentItemViewModel(), new ContentItemErrorViewModel { Message = msg });

    // ── Mapping ───────────────────────────────────────────────────────

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
