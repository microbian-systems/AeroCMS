using System.Text.Json;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Content.Serialization;
using Aero.Cms.Abstractions.Content.Views;
using Aero.Cms.Core.Infrastructure;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Core.Content.Rendering;

/// <summary>Resolves configured virtual references into a bounded, detached render scope.</summary>
public interface IContentRenderReferenceResolver
{
    Task<Result<JsonElement, AeroError>> ResolveAsync(
        ContentTypeDefinition typeDefinition,
        ContentItem item,
        CancellationToken ct = default);
}

/// <summary>
/// Uses only provider-qualified keys already stored on the item. The public template receives
/// configured projection fields; it never receives a provider, session, or arbitrary CLR object.
/// </summary>
public sealed class ContentRenderReferenceResolver(
    IContentEntrySourceProviderCatalog catalog,
    ISelectedSiteScopeResolver scopeResolver) : IContentRenderReferenceResolver
{
    private const int MaximumResolvedReferences = 16;

    public async Task<Result<JsonElement, AeroError>> ResolveAsync(
        ContentTypeDefinition typeDefinition,
        ContentItem item,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(typeDefinition);
        ArgumentNullException.ThrowIfNull(item);
        var selected = await scopeResolver.ResolveAsync(item.SiteId, ct);
        if (selected is not { IsValid: true } || selected.Value.SiteId != item.SiteId)
            return AeroError.ValidationError(["The content render site scope could not be resolved."]);
        var scope = new ContentViewScope(selected.Value.TenantId, selected.Value.SiteId);
        var resolved = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        var referenceFields = typeDefinition.Fields.Where(IsVirtualReference).ToArray();
        if (referenceFields.Length > MaximumResolvedReferences)
            return AeroError.ValidationError([$"Content rendering supports at most {MaximumResolvedReferences} virtual references."]);

        foreach (var field in referenceFields)
        {
            if (!item.Fields.TryGetValue(field.Name, out var value)
                || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                if (field.Required) return AeroError.ValidationError([$"Required reference '{field.Label ?? field.Name}' is unavailable for rendering."]);
                continue;
            }

            ContentEntryKey? key;
            try { key = value.Deserialize(ContentJsonContext.Default.ContentEntryKey); }
            catch (JsonException) { key = null; }
            if (key is not { IsValid: true } validKey || !IsAllowedProvider(field, validKey.Provider))
                return AeroError.ValidationError([$"Reference '{field.Label ?? field.Name}' is invalid for rendering."]);

            var provider = await catalog.ResolveAsync(scope, validKey.Provider, ct);
            var entry = provider is null ? null : await provider.FindAsync(scope, validKey.StableId, ct);
            if (entry is null || entry.Scope != scope || entry.Key != validKey)
                return AeroError.ValidationError([$"Reference '{field.Label ?? field.Name}' could not be resolved in the current site."]);

            var projection = new SortedDictionary<string, object?>(StringComparer.Ordinal);
            foreach (var name in PreviewFields(field))
                if (!IsReservedIdentityField(name) && entry.Values.TryGetValue(name, out var projected))
                    projection[name] = projected;
            projection["provider"] = validKey.Provider;
            projection["stableId"] = validKey.StableId;
            resolved[field.Name] = projection;
        }

        return JsonSerializer.SerializeToElement(resolved);
    }

    private static bool IsVirtualReference(ContentFieldDefinition field)
        => string.Equals(field.FieldType, ContentFieldTypes.Reference, StringComparison.Ordinal)
           && field.Settings.TryGetValue(ReferenceContentFieldSettings.TargetKind, out var target)
           && target.ValueKind == JsonValueKind.String
           && string.Equals(target.GetString(), ReferenceContentFieldSettings.TargetKindContentEntry, StringComparison.Ordinal);

    private static bool IsAllowedProvider(ContentFieldDefinition field, string provider)
        => !field.Settings.TryGetValue(ReferenceContentFieldSettings.AllowedProviders, out var allowed)
           || allowed.ValueKind == JsonValueKind.Array
           && allowed.EnumerateArray().Any(item => item.ValueKind == JsonValueKind.String
               && string.Equals(item.GetString(), provider, StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<string> PreviewFields(ContentFieldDefinition field)
        => field.Settings.TryGetValue(ReferenceContentFieldSettings.PreviewFields, out var fields)
           && fields.ValueKind == JsonValueKind.Array
            ? fields.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .Distinct(StringComparer.Ordinal)
                .Take(16)
            : [];

    private static bool IsReservedIdentityField(string name)
        => string.Equals(name, "provider", StringComparison.Ordinal)
           || string.Equals(name, "stableId", StringComparison.Ordinal);
}
