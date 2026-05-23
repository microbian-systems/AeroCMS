using System.Text.Json;
using Aero.Actors;
using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Blocks.Serialization;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Abstractions.Models;
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

    public AeroContentItemGrain(
        ILogger<AeroActor> log,
        IServiceScopeFactory scopeFactory)
        : base(log)
    {
        _scopeFactory = scopeFactory;
    }

    // ── IHaveState<ContentItemViewModel> ─────────────────────────────

    public Task<ContentItemViewModel> GetStateAsync(CancellationToken ct)
        => Task.FromResult(_state);

    public Task UpdateStateAsync(ContentItemViewModel state, CancellationToken ct)
    {
        _state = state;
        return Task.CompletedTask;
    }

    // ── ICruddable<ContentItemViewModel, long> ───────────────────────

    public async Task<AeroRequestResponse<ContentItemViewModel>> GetByIdAsync(long id, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var contentService = scope.ServiceProvider.GetRequiredService<IContentService>();

        var result = await contentService.LoadAsync(id, ct);
        return result switch
        {
            Result<ContentItem, AeroError>.Ok ok => Ok(MapToViewModel(ok.Value)),
            _ => NotFound($"Content item {id} not found")
        };
    }

    public Task<AeroRequestResponse<ContentItemViewModel>> GetByIdsAsync(long[] ids, CancellationToken ct)
        => Task.FromResult(Fail("GetByIdsAsync not supported for content items"));

    public async Task<AeroRequestResponse<ContentItemViewModel>> CreateAsync(IRequest request, CancellationToken ct)
    {
        // Content items don't use the standard IRequest pattern — Create is handled
        // via SaveDraftAsync with a fully constructed ContentItem.
        return Fail("Use CreateContentItemAsync instead of CreateAsync for content items");
    }

    public Task<AeroRequestResponse<ContentItemViewModel>> UpdateAsync(IRequest request, CancellationToken ct)
        => Task.FromResult(Fail("Use SaveDraftAsync directly for content items"));

    public async Task<AeroRequestResponse<ContentItemViewModel>> DeleteAsync(IRequest request, CancellationToken ct)
    {
        // Content items use DeleteAsync(long id) instead of the IRequest pattern.
        // This overload exists to satisfy ICruddable<ContentItemViewModel, long>.
        return Fail("Use DeleteAsync(long id) for content items");
    }

    public async Task<AeroRequestResponse<ContentItemViewModel>> DeleteAsync(long id, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var commandService = scope.ServiceProvider.GetRequiredService<ContentCommandService>();

        var result = await commandService.DeleteAsync(id, ct);
        return result switch
        {
            Result<bool, AeroError>.Ok => Ok(new ContentItemViewModel()),
            _ => Fail("Delete failed")
        };
    }

    // ── ICanFindBySite / ICanFindBySlug — stubbed ────────────────────

    public Task<AeroRequestResponse<ContentItemViewModel>> GetBySiteIdAsync(long siteId, int page = 1, int rows = 10, CancellationToken ct = default)
        => Task.FromResult(Fail("Not supported"));

    public Task<AeroRequestResponse<ContentItemViewModel>> GetBySlugAsync(long siteId, string slug, CancellationToken ct)
        => Task.FromResult(Fail("Not supported"));

    Task<AeroRequestResponse<ContentItemViewModel>> ICanFindBySlug<ContentItemViewModel, string>.GetBySlugAsync(string siteId, string slug, CancellationToken ct)
        => Task.FromResult(Fail("Not supported"));

    // ── IAeroContentItemActor content-specific methods ────────────────

    public async Task<(List<ContentItemViewModel> Items, long TotalCount)> GetByTypeAsync(
        long siteId, string contentTypeAlias, int skip, int take, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var queryService = scope.ServiceProvider.GetRequiredService<IContentQueryService>();

        var result = await queryService.GetByTypeAsync(siteId, contentTypeAlias, skip, take, ct);
        return result switch
        {
            Result<(IReadOnlyList<ContentItem> Items, long TotalCount), AeroError>.Ok ok =>
                (ok.Value.Items.Select(MapToViewModel).ToList(), ok.Value.TotalCount),
            _ => ([], 0)
        };
    }

    public async Task<AeroRequestResponse<ContentItemViewModel>> SaveDraftAsync(ContentItemViewModel vm, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var commandService = scope.ServiceProvider.GetRequiredService<ContentCommandService>();

        var item = ToEntity(vm);
        var result = await commandService.SaveDraftAsync(item, ct);
        return result switch
        {
            Result<ContentItem, AeroError>.Ok ok => Ok(MapToViewModel(ok.Value)),
            Result<ContentItem, AeroError>.Failure failure => Fail(failure.Error.ToString()),
            _ => Fail("Unexpected result")
        };
    }

    public async Task<AeroRequestResponse<ContentItemViewModel>> PublishAsync(long id, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
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

    public async Task<AeroRequestResponse<ContentItemViewModel>> UnpublishAsync(long id, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
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
        FieldsJson = JsonSerializer.Serialize(item.Fields, BlockJsonContext.Default.Options),
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
            : JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(vm.FieldsJson, BlockJsonContext.Default.Options)
              ?? new Dictionary<string, JsonElement>();

        return new ContentItem
        {
            Id = vm.Id != 0 ? vm.Id : Snowflake.NewId(),
            SiteId = vm.SiteId,
            ContentTypeAlias = vm.ContentTypeAlias,
            Title = vm.Title,
            Slug = vm.Slug,
            Fields = fields,
            PublicationState = vm.PublicationState,
            PublishedOn = vm.PublishedOn,
            VersionNumber = vm.VersionNumber,
            SchedulePublishUtc = vm.SchedulePublishUtc,
            ScheduleUnpublishUtc = vm.ScheduleUnpublishUtc
        };
    }
}
