using System.Text.Json;
using Aero.Actors;
using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Content.Serialization;
using Aero.Cms.Abstractions.Events;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Modules.Content.Events;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Modules.Content.Grains;

/// <summary>
/// Orleans grain for content type definition management — wraps existing
/// <see cref="IContentTypeService"/> via <see cref="IServiceScopeFactory"/>.
/// </summary>
public sealed class AeroContentTypeGrain : AeroActor, IAeroContentTypeActor
{
    private readonly IServiceScopeFactory _scopeFactory;

        /// <summary>
    /// Initializes a new instance of the <see cref="AeroContentTypeGrain"/> class.
    /// </summary>
public AeroContentTypeGrain(
        ILogger<AeroActor> log,
        IServiceScopeFactory scopeFactory)
        : base(log)
    {
        _scopeFactory = scopeFactory;
    }

        /// <summary>
    /// GetAllAsync method.
    /// </summary>
public async Task<List<ContentTypeViewModel>> GetAllAsync(long siteId, CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IContentTypeService>();

        var result = await service.GetAllAsync(siteId, ct);
        return result switch
        {
            Result<IReadOnlyList<ContentTypeDefinition>, AeroError>.Ok ok =>
                ok.Value.Select(MapToViewModel).ToList(),
            _ => []
        };
    }

        /// <summary>
    /// GetByAliasAsync method.
    /// </summary>
public async Task<ContentTypeViewModel?> GetByAliasAsync(long siteId, string alias, CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IContentTypeService>();

        var result = await service.GetByAliasAsync(siteId, alias, ct);
        return result switch
        {
            Result<ContentTypeDefinition, AeroError>.Ok ok => MapToViewModel(ok.Value),
            _ => null
        };
    }

        /// <summary>
    /// CreateAsync method.
    /// </summary>
public async Task<AeroRequestResponse<ContentTypeViewModel>> CreateAsync(ContentTypeViewModel vm, CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IContentTypeService>();

        var definition = ToEntity(vm, isNew: true);
        var result = await service.SaveAsync(definition, ct);

        if (result is Result<ContentTypeDefinition, AeroError>.Ok ok)
        {
            var viewModel = MapToViewModel(ok.Value);
            var events = scope.ServiceProvider.GetRequiredService<ContentEventPublisher>();
            await events.PublishBestEffortAsync(new ContentTypeViewModelCreated(viewModel));
            return Ok(viewModel);
        }

        return result switch
        {
            Result<ContentTypeDefinition, AeroError>.Failure failure => Fail(failure.Error.ToString()),
            _ => Fail("Unexpected result")
        };
    }

        /// <summary>
    /// UpdateAsync method.
    /// </summary>
public async Task<AeroRequestResponse<ContentTypeViewModel>> UpdateAsync(ContentTypeViewModel vm, CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IContentTypeService>();

        var definition = ToEntity(vm, isNew: false);
        var result = await service.SaveAsync(definition, ct);

        if (result is Result<ContentTypeDefinition, AeroError>.Ok ok)
        {
            var viewModel = MapToViewModel(ok.Value);
            var events = scope.ServiceProvider.GetRequiredService<ContentEventPublisher>();
            await events.PublishBestEffortAsync(new ContentTypeViewModelUpdated(viewModel));
            return Ok(viewModel);
        }

        return result switch
        {
            Result<ContentTypeDefinition, AeroError>.Failure failure => Fail(failure.Error.ToString()),
            _ => Fail("Unexpected result")
        };
    }

        /// <summary>
    /// DeleteAsync method.
    /// </summary>
public async Task<bool> DeleteAsync(long siteId, string alias, CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IContentTypeService>();

        var existing = await service.GetByAliasAsync(siteId, alias, ct);
        if (existing is not Result<ContentTypeDefinition, AeroError>.Ok ok)
            return false;

        var result = await service.DeleteAsync(siteId, alias, ct);
        if (result is Result<bool, AeroError>.Ok deleteOk && deleteOk.Value)
        {
            var viewModel = MapToViewModel(ok.Value);
            var events = scope.ServiceProvider.GetRequiredService<ContentEventPublisher>();
            await events.PublishBestEffortAsync(new ContentTypeViewModelDeleted(viewModel));
            return true;
        }

        return false;
    }

    // ── AeroRequestResponse helpers ────────────────────────────────────

    private static AeroRequestResponse<ContentTypeViewModel> Ok(ContentTypeViewModel vm)
        => new(vm, new ContentTypeErrorViewModel());

    private static AeroRequestResponse<ContentTypeViewModel> Fail(string msg)
        => new(new ContentTypeViewModel(), new ContentTypeErrorViewModel { Message = msg });

    // ── Mapping ───────────────────────────────────────────────────────

    private static ContentTypeViewModel MapToViewModel(ContentTypeDefinition def) => new()
    {
        Id = def.Id,
        SiteId = def.SiteId,
        Alias = def.Alias,
        Name = def.Name,
        Description = def.Description,
        Category = def.Category,
        Icon = def.Icon,
        AllowPublicUrl = def.AllowPublicUrl,
        HideFromSearch = def.HideFromSearch,
        FieldsJson = JsonSerializer.Serialize(
            def.Fields,
            ContentJsonContext.Default.ListContentFieldDefinition),
        ScribanTemplate = def.ScribanTemplate,
        ScheduleConfig = def.ScheduleConfig
    };

    private static ContentTypeDefinition ToEntity(ContentTypeViewModel vm, bool isNew)
    {
        var fields = string.IsNullOrWhiteSpace(vm.FieldsJson) || vm.FieldsJson == "[]"
            ? []
            : JsonSerializer.Deserialize(
                vm.FieldsJson,
                ContentJsonContext.Default.ListContentFieldDefinition) ?? [];

        return new ContentTypeDefinition
        {
            Id = isNew ? Snowflake.NewId() : vm.Id,
            SiteId = vm.SiteId,
            Alias = vm.Alias,
            Name = vm.Name,
            Description = vm.Description,
            Category = vm.Category,
            Icon = vm.Icon,
            AllowPublicUrl = vm.AllowPublicUrl,
            HideFromSearch = vm.HideFromSearch,
            Fields = fields,
            ScribanTemplate = vm.ScribanTemplate,
            ScheduleConfig = vm.ScheduleConfig
        };
    }
}
