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
/// <remarks>
/// Site identity is supplied explicitly by each caller and is forced onto mutations. Successful
/// mutations publish non-durable notifications after persistence.
/// </remarks>
public sealed class AeroContentTypeGrain : AeroActor, IAeroContentTypeActor
{
    private readonly IServiceScopeFactory _scopeFactory;

        /// <summary>
    /// Initializes the grain with its actor logger and service-scope factory.
    /// </summary>
    /// <param name="log">The logger forwarded to the actor base.</param>
    /// <param name="scopeFactory">The factory used to isolate scoped services per operation.</param>
public AeroContentTypeGrain(
        ILogger<AeroActor> log,
        IServiceScopeFactory scopeFactory)
        : base(log)
    {
        _scopeFactory = scopeFactory;
    }

        /// <summary>
    /// Lists definitions for a caller-supplied site.
    /// </summary>
    /// <returns>Mapped definitions, or an empty list for any railway failure.</returns>
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
    /// Loads a site-scoped definition by alias.
    /// </summary>
    /// <returns>The mapped definition, or <see langword="null"/> for any railway failure.</returns>
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
    /// Assigns a new Snowflake identifier, saves a definition, and publishes a created notification.
    /// </summary>
    /// <remarks>The explicit site argument is authoritative; the view-model site is overwritten.</remarks>
public async Task<AeroRequestResponse<ContentTypeViewModel>> CreateAsync(ContentTypeViewModel vm, long siteId, CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IContentTypeService>();

        vm.Id = 0;
        vm.SiteId = siteId;
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
            Result<ContentTypeDefinition, AeroError>.Failure failure => Fail(GetErrorMessage(failure.Error)),
            _ => Fail("Unexpected result")
        };
    }

        /// <summary>
    /// Saves a definition under the selected site and publishes an updated notification.
    /// </summary>
    /// <remarks>The service rejects missing or foreign identifiers and enforces per-site alias uniqueness.</remarks>
public async Task<AeroRequestResponse<ContentTypeViewModel>> UpdateAsync(ContentTypeViewModel vm, long siteId, CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IContentTypeService>();

        vm.SiteId = siteId;
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
            Result<ContentTypeDefinition, AeroError>.Failure failure => Fail(GetErrorMessage(failure.Error)),
            _ => Fail("Unexpected result")
        };
    }

        /// <summary>
    /// Deletes a site-scoped definition and publishes its pre-delete representation.
    /// </summary>
    /// <returns><see langword="true"/> only when lookup and deletion both succeed.</returns>
public async Task<Result<bool, AeroError>> DeleteAsync(long siteId, string alias, CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IContentTypeService>();

        var existing = await service.GetByAliasAsync(siteId, alias, ct);
        if (existing is not Result<ContentTypeDefinition, AeroError>.Ok ok)
            return existing is Result<ContentTypeDefinition, AeroError>.Failure failure
                ? failure.Error
                : AeroError.NotFoundError($"Content type '{alias}' not found.");

        var result = await service.DeleteAsync(siteId, alias, ct);
        if (result is Result<bool, AeroError>.Ok deleteOk && deleteOk.Value)
        {
            var viewModel = MapToViewModel(ok.Value);
            var events = scope.ServiceProvider.GetRequiredService<ContentEventPublisher>();
            await events.PublishBestEffortAsync(new ContentTypeViewModelDeleted(viewModel));
            return true;
        }

        return result;
    }

    // ── AeroRequestResponse helpers ────────────────────────────────────

    /// <summary>Creates a response with data and an empty error model.</summary>
    private static AeroRequestResponse<ContentTypeViewModel> Ok(ContentTypeViewModel vm)
        => new(vm, new ContentTypeErrorViewModel());

    /// <summary>Creates an empty-data response carrying a failure message.</summary>
    private static AeroRequestResponse<ContentTypeViewModel> Fail(string msg)
        => new(new ContentTypeViewModel(), new ContentTypeErrorViewModel { Message = msg });

    /// <summary>Extracts a human-readable message from a railway error.</summary>
    private static string GetErrorMessage(AeroError error) => error switch
    {
        AeroError.Error e => e.msg,
        AeroError.NotFound e => e.msg,
        AeroError.Conflict e => e.msg,
        AeroError.Database e => e.msg,
        AeroError.Unauthorized e => e.msg,
        AeroError.Forbidden e => e.msg,
        AeroError.Timeout e => e.msg,
        AeroError.InvalidRequest e => e.msg,
        AeroError.BadRequest e => e.msg,
        AeroError.Exists e => e.msg,
        AeroError.NullReferro e => e.msg,
        AeroError.Cancelled e => e.msg,
        AeroError.NotAllowed e => e.msg,
        AeroError.Configuration e => e.msg,
        AeroError.Validation e => string.Join("; ", e.Errors),
        AeroError.HttpRequest e => e.msg ?? "HTTP request error",
        _ => error.ToString()
    };

    // ── Mapping ───────────────────────────────────────────────────────

    /// <summary>Serializes fields and projects a definition into its actor contract.</summary>
    private static ContentTypeViewModel MapToViewModel(ContentTypeDefinition def) => new()
    {
        Id = def.Id,
        SiteId = def.SiteId,
        Alias = def.Alias,
        Name = def.Name,
        Description = def.Description,
        Category = def.Category,
        Icon = def.Icon,
        Cardinality = def.Cardinality,
        Structure = def.Structure,
        HierarchyRules = def.HierarchyRules,
        AllowPublicUrl = def.AllowPublicUrl,
        IncludeInSearch = def.IncludeInSearch,
        IncludeInPublicAi = def.IncludeInPublicAi,
        Localization = def.Localization,
        FieldsJson = JsonSerializer.Serialize(
            def.Fields,
            ContentJsonContext.Default.ListContentFieldDefinition),
        ScribanTemplate = def.ScribanTemplate,
        ScheduleConfig = def.ScheduleConfig
    };

    /// <summary>
    /// Deserializes field definitions and projects a view model into a persistence entity.
    /// </summary>
    /// <remarks>A new Snowflake identifier is assigned only when <paramref name="isNew"/> is true.</remarks>
    private static ContentTypeDefinition ToEntity(ContentTypeViewModel vm, bool isNew)
    {
        var fields = string.IsNullOrWhiteSpace(vm.FieldsJson) || vm.FieldsJson == "[]"
            ? []
            : JsonSerializer.Deserialize(
                vm.FieldsJson,
                ContentJsonContext.Default.ListContentFieldDefinition) ?? [];

        return new ContentTypeDefinition
        {
            Id = isNew ? 0 : vm.Id,
            SiteId = vm.SiteId,
            Alias = vm.Alias,
            Name = vm.Name,
            Description = vm.Description,
            Category = vm.Category,
            Icon = vm.Icon,
            Cardinality = vm.Cardinality,
            Structure = vm.Structure,
            HierarchyRules = vm.HierarchyRules,
            AllowPublicUrl = vm.AllowPublicUrl,
            IncludeInSearch = vm.IncludeInSearch,
            IncludeInPublicAi = vm.IncludeInPublicAi,
            Localization = vm.Localization,
            Fields = fields,
            ScribanTemplate = vm.ScribanTemplate,
            ScheduleConfig = vm.ScheduleConfig
        };
    }
}
