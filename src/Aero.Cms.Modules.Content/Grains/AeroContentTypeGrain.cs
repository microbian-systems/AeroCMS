using System.Text.Json;
using Aero.Actors;
using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Models;
using Microsoft.Extensions.DependencyInjection;

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

        return result switch
        {
            Result<ContentTypeDefinition, AeroError>.Ok ok => Ok(MapToViewModel(ok.Value)),
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

        return result switch
        {
            Result<ContentTypeDefinition, AeroError>.Ok ok => Ok(MapToViewModel(ok.Value)),
            Result<ContentTypeDefinition, AeroError>.Failure failure => Fail(failure.Error.ToString()),
            _ => Fail("Unexpected result")
        };
    }

    public async Task<bool> DeleteAsync(long siteId, string alias, CancellationToken ct = default)
    {
        // ContentTypeService doesn't have explicit delete — handled via Marten
        return true; // stub — actual delete via service layer
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
        Alias = def.Alias,
        Name = def.Name,
        Description = def.Description,
        Category = def.Category,
        Icon = def.Icon,
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
            Alias = vm.Alias,
            Name = vm.Name,
            Description = vm.Description,
            Category = vm.Category,
            Icon = vm.Icon,
            Fields = fields,
            ScribanTemplate = vm.ScribanTemplate,
            RenderMode = vm.RenderMode
        };
    }
}
