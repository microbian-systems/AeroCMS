using System.Text.Json;
using Aero.Actors;
using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Events;
using Aero.Cms.Abstractions.Models;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Aero.Cms.Modules.Content.Grains;

/// <summary>
/// Orleans grain for content type definition management — wraps existing
/// <see cref="IContentTypeService"/> via <see cref="IServiceScopeFactory"/>.
/// </summary>
public sealed class AeroContentTypeGrain : AeroActor, IAeroContentTypeActor
{
    private readonly IServiceScopeFactory _scopeFactory;

    public AeroContentTypeGrain(
        ILogger<AeroActor> log,
        IServiceScopeFactory scopeFactory)
        : base(log)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<List<ContentTypeViewModel>> GetAllAsync(long siteId, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IContentTypeService>();

        var result = await service.GetAllAsync(siteId, ct);
        return result switch
        {
            Result<IReadOnlyList<ContentTypeDefinition>, AeroError>.Ok ok =>
                ok.Value.Select(MapToViewModel).ToList(),
            _ => []
        };
    }

    public async Task<ContentTypeViewModel?> GetByAliasAsync(long siteId, string alias, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IContentTypeService>();

        var result = await service.GetByAliasAsync(siteId, alias, ct);
        return result switch
        {
            Result<ContentTypeDefinition, AeroError>.Ok ok => MapToViewModel(ok.Value),
            _ => null
        };
    }

    public async Task<AeroRequestResponse<ContentTypeViewModel>> CreateAsync(ContentTypeViewModel vm, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IContentTypeService>();

        var definition = ToEntity(vm, isNew: true);
        var result = await service.SaveAsync(definition, ct);

        if (result is Result<ContentTypeDefinition, AeroError>.Ok ok)
        {
            var viewModel = MapToViewModel(ok.Value);
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            await bus.PublishAsync(new ContentTypeViewModelCreated(viewModel));
            return Ok(viewModel);
        }

        return result switch
        {
            Result<ContentTypeDefinition, AeroError>.Failure failure => Fail(failure.Error.ToString()),
            _ => Fail("Unexpected result")
        };
    }

    public async Task<AeroRequestResponse<ContentTypeViewModel>> UpdateAsync(ContentTypeViewModel vm, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IContentTypeService>();

        var definition = ToEntity(vm, isNew: false);
        var result = await service.SaveAsync(definition, ct);

        if (result is Result<ContentTypeDefinition, AeroError>.Ok ok)
        {
            var viewModel = MapToViewModel(ok.Value);
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            await bus.PublishAsync(new ContentTypeViewModelUpdated(viewModel));
            return Ok(viewModel);
        }

        return result switch
        {
            Result<ContentTypeDefinition, AeroError>.Failure failure => Fail(failure.Error.ToString()),
            _ => Fail("Unexpected result")
        };
    }

    public async Task<bool> DeleteAsync(long siteId, string alias, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IContentTypeService>();

        var existing = await service.GetByAliasAsync(siteId, alias, ct);
        if (existing is not Result<ContentTypeDefinition, AeroError>.Ok ok)
            return false;

        var result = await service.DeleteAsync(siteId, alias, ct);
        if (result is Result<bool, AeroError>.Ok deleteOk && deleteOk.Value)
        {
            var viewModel = MapToViewModel(ok.Value);
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            await bus.PublishAsync(new ContentTypeViewModelDeleted(viewModel));
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
        FieldsJson = JsonSerializer.Serialize(def.Fields),
        ScribanTemplate = def.ScribanTemplate,
        RenderMode = def.RenderMode
    };

    private static ContentTypeDefinition ToEntity(ContentTypeViewModel vm, bool isNew)
    {
        var fields = string.IsNullOrWhiteSpace(vm.FieldsJson) || vm.FieldsJson == "[]"
            ? []
            : JsonSerializer.Deserialize<List<ContentFieldDefinition>>(vm.FieldsJson) ?? [];

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
            RenderMode = vm.RenderMode
        };
    }
}
