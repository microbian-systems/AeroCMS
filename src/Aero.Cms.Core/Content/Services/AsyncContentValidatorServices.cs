using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Content.Serialization;
using Aero.Cms.Abstractions.Content.Views;
using Aero.Core;
using Aero.Core.Railway;
using Aero.Core.Http;
using AeroDB.Sable;
using FluentValidation.Results;
using System.Globalization;
using System.Text.Json;

namespace Aero.Cms.Core.Content.Services;

/// <summary>
/// Checks whether a site's slug is already assigned to a different content item.
/// </summary>
/// <remarks>
/// The check is scoped to site, content type, and culture. It is an application-time
/// lookup and does not guarantee race-free uniqueness. Lookup failures represented as
/// <see cref="AeroError"/> values are treated as no conflict.
/// </remarks>
public sealed class UniqueSlugValidator(IContentService contentService) : IAsyncContentValidator
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<ValidationFailure>> ValidateAsync(ContentItem item, ContentTypeDefinition type, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(item.Slug))
            return [];

        var existingResult = await contentService.GetBySlugAndTypeAsync(
            item.SiteId, item.ContentTypeAlias, item.Culture, item.Slug, ct);
        if (existingResult is Result<ContentItem, AeroError>.Ok ok && ok.Value.Id != item.Id)
            return [new ValidationFailure(nameof(item.Slug), $"Slug '{item.Slug}' is already in use.")];

        return [];
    }
}

/// <summary>
/// Verifies that parseable referenced content identifiers exist and satisfy their configured target.
/// </summary>
/// <remarks>
/// Only fields whose type is exactly <c>reference</c> are inspected. The validator honors a
/// Boolean <c>allowMultiple</c> setting. Configured targets are resolved by immutable content-type ID,
/// and hierarchy references can require a leaf entry. Non-parseable identifiers are expected to be
/// rejected by synchronous field validation and do not produce an existence failure here. When
/// invoked outside <see cref="ContentValidationService"/>, incorrectly shaped JSON may cause
/// <see cref="InvalidOperationException"/> while enumerating or reading reference values.
/// </remarks>
public sealed class ReferenceExistenceValidator(
    IContentService contentService,
    IDocumentSession session,
    IEnumerable<IContentReferenceSourceProvider>? sourceProviders = null,
    IContentTypeService? contentTypeService = null,
    IEnumerable<IContentEntrySourceProvider>? entryProviders = null,
    IContentEntrySourceProviderCatalog? entryProviderCatalog = null,
    ISiteContext? siteContext = null) : IAsyncContentValidator
{
    private readonly IReadOnlyDictionary<string, IContentReferenceSourceProvider>
        cmsSourceProviders = (sourceProviders ?? [])
            .ToDictionary(
                provider => provider.SourceKey,
                StringComparer.OrdinalIgnoreCase);
    private readonly IReadOnlyDictionary<string, IContentEntrySourceProvider> entryProvidersByKey = (entryProviders ?? [])
        .ToDictionary(provider => provider.Provider, StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ValidationFailure>> ValidateAsync(ContentItem item, ContentTypeDefinition type, CancellationToken ct)
    {
        var failures = new List<ValidationFailure>();

        foreach (var field in type.Fields.Where(f => f.FieldType == "reference"))
        {
            if (!item.Fields.TryGetValue(field.Name, out var element)) continue;
            if (element.ValueKind == System.Text.Json.JsonValueKind.Null) continue;

            if (ReferenceFieldValidator.IsCmsDocumentReference(field))
            {
                await CheckCmsDocumentReference(
                    item,
                    element,
                    field,
                    failures,
                    ct);
                continue;
            }

            if (ReferenceFieldValidator.IsContentEntryReference(field))
            {
                await CheckContentEntryReference(item, element, field, failures, ct);
                continue;
            }

            if (field.Settings.TryGetValue(ReferenceContentFieldSettings.AllowMultiple, out var multiple)
                && multiple.ValueKind == System.Text.Json.JsonValueKind.True)
            {
                foreach (var refItem in element.EnumerateArray())
                    await CheckReference(item, refItem, field, failures, ct);
            }
            else
            {
                await CheckReference(item, element, field, failures, ct);
            }
        }

        return failures;
    }

    private async Task CheckContentEntryReference(
        ContentItem item,
        System.Text.Json.JsonElement element,
        ContentFieldDefinition field,
        List<ValidationFailure> failures,
        CancellationToken ct)
    {
        ContentEntryKey? key;
        try
        {
            key = element.Deserialize(ContentJsonContext.Default.ContentEntryKey);
        }
        catch (System.Text.Json.JsonException)
        {
            return;
        }

        if (key is not { IsValid: true }) return;
        var allowedProviders = ReferenceFieldValidator.GetAllowedProviders(field);
        if (allowedProviders.Count > 0 && !allowedProviders.Contains(key.Value.Provider, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        var scope = new ContentViewScope(siteContext?.TenantId ?? 0, item.SiteId);
        if (!scope.IsValid)
        {
            failures.Add(new ValidationFailure(field.Name, "Content-entry references require a tenant and site scope."));
            return;
        }

        var provider = entryProvidersByKey.TryGetValue(key.Value.Provider, out var registered)
            ? registered
            : entryProviderCatalog is null
                ? null
                : await entryProviderCatalog.ResolveAsync(scope, key.Value.Provider, ct);
        if (provider is null || !string.Equals(provider.Provider, key.Value.Provider, StringComparison.OrdinalIgnoreCase))
        {
            failures.Add(new ValidationFailure(field.Name, $"The '{key.Value.Provider}' content-entry provider is unavailable."));
            return;
        }

        var entry = await provider.FindAsync(scope, key.Value.StableId, ct);
        if (entry is null
            || entry.Scope != scope
            || entry.Key != key.Value
            || !entry.Key.IsValid)
        {
            failures.Add(new ValidationFailure(field.Name, $"Referenced content entry '{key.Value.StableId}' was not found."));
        }
    }

    private async Task CheckCmsDocumentReference(
        ContentItem item,
        System.Text.Json.JsonElement element,
        ContentFieldDefinition field,
        List<ValidationFailure> failures,
        CancellationToken ct)
    {
        if (element.ValueKind != System.Text.Json.JsonValueKind.Object
            || !element.TryGetProperty("source", out var sourceElement)
            || sourceElement.ValueKind != System.Text.Json.JsonValueKind.String
            || !element.TryGetProperty("id", out var idElement)
            || idElement.ValueKind != System.Text.Json.JsonValueKind.String
            || !long.TryParse(
                idElement.GetString(),
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out var id))
        {
            return;
        }

        var source = sourceElement.GetString()!;
        if (CmsContentReferenceSources.TryGetContentTypeAlias(
                source,
                out var contentTypeAlias))
        {
            await CheckContentItemPageReference(
                item,
                field,
                id,
                contentTypeAlias,
                failures,
                ct);
            return;
        }

        if (!cmsSourceProviders.TryGetValue(source, out var provider))
        {
            failures.Add(new ValidationFailure(
                field.Name,
                $"The '{source}' reference source is unavailable."));
            return;
        }

        var result = await provider.ExistsAsync(item.SiteId, id, ct);
        switch (result)
        {
            case Result<bool>.Ok { Value: true }:
                return;
            case Result<bool>.Ok:
                failures.Add(new ValidationFailure(
                    field.Name,
                    $"Referenced {provider.DisplayName.ToLowerInvariant()} item '{id}' was not found."));
                return;
            case Result<bool>.Failure failure:
                failures.Add(new ValidationFailure(
                    field.Name,
                    failure.Error.ToString()));
                return;
        }
    }

    private async Task CheckContentItemPageReference(
        ContentItem item,
        ContentFieldDefinition field,
        long id,
        string contentTypeAlias,
        List<ValidationFailure> failures,
        CancellationToken ct)
    {
        if (contentTypeService is null)
        {
            failures.Add(new ValidationFailure(
                field.Name,
                "Public content-entry references are unavailable."));
            return;
        }

        var targetType = await contentTypeService.GetByAliasAsync(
            item.SiteId,
            contentTypeAlias,
            ct);
        if (targetType is not Result<ContentTypeDefinition, AeroError>.Ok
            {
                Value: { AllowPublicUrl: true }
            })
        {
            failures.Add(new ValidationFailure(
                field.Name,
                $"The '{contentTypeAlias}' public content type was not found."));
            return;
        }

        var referenced = await contentService.LoadAsync(item.SiteId, id, ct);
        if (referenced is not Result<ContentItem, AeroError>.Ok ok
            || !string.Equals(
                ok.Value.ContentTypeAlias,
                contentTypeAlias,
                StringComparison.Ordinal))
        {
            failures.Add(new ValidationFailure(
                field.Name,
                $"Referenced {contentTypeAlias} entry '{id}' was not found."));
        }
    }

    private async Task CheckReference(
        ContentItem item,
        System.Text.Json.JsonElement element,
        ContentFieldDefinition field,
        List<ValidationFailure> failures,
        CancellationToken ct)
    {
        if (!TryReadReferenceId(element, out var id))
        {
            return;
        }

        var referenced = await contentService.LoadAsync(item.SiteId, id, ct);
        if (referenced is not Result<ContentItem, AeroError>.Ok ok)
        {
            failures.Add(new ValidationFailure(
                field.Name,
                $"Referenced item '{id}' for '{field.Label ?? field.Name}' not found."));
            return;
        }

        if (!TryGetTargetContentTypeId(field, out var targetContentTypeId))
        {
            failures.Add(new ValidationFailure(
                field.Name,
                "The reference field has no valid target content-type identifier."));
            return;
        }

        if (contentTypeService is null)
        {
            failures.Add(new ValidationFailure(field.Name, "Content type resolution is unavailable."));
            return;
        }

        var target = await contentTypeService.GetByIdAsync(item.SiteId, targetContentTypeId, ct);
        if (target is not Result<ContentTypeDefinition, AeroError>.Ok targetOk
            || !string.Equals(
                ok.Value.ContentTypeAlias,
                targetOk.Value.Alias,
                StringComparison.Ordinal))
        {
            failures.Add(new ValidationFailure(
                field.Name,
                $"Referenced item '{id}' is not an entry of the configured content type."));
            return;
        }

        var dependsOnField = GetStringSetting(
            field,
            ReferenceContentFieldSettings.DependsOnField);
        var targetFilterField = GetStringSetting(
            field,
            ReferenceContentFieldSettings.TargetFilterField);
        if (!string.IsNullOrWhiteSpace(dependsOnField)
            && !string.IsNullOrWhiteSpace(targetFilterField))
        {
            if (!item.Fields.TryGetValue(dependsOnField, out var dependency)
                || !ok.Value.Fields.TryGetValue(targetFilterField, out var targetRelationship)
                || !ReferenceValuesOverlap(dependency, targetRelationship))
            {
                failures.Add(new ValidationFailure(
                    field.Name,
                    $"Referenced item '{id}' does not belong to the selected '{dependsOnField}' entry."));
                return;
            }
        }

        if (!IsTrue(field, ReferenceContentFieldSettings.SelectLeafOnly)
            || !IsHierarchyReference(field))
        {
            return;
        }

        var hasChildren = await session.Query<ContentItem>()
            .Where(candidate =>
                candidate.SiteId == item.SiteId
                && candidate.ParentId == id)
            .AnyAsync(ct);
        if (hasChildren)
        {
            failures.Add(new ValidationFailure(
                field.Name,
                $"Referenced item '{id}' must be a leaf entry without children."));
        }
    }

    private static bool ReferenceValuesOverlap(
        System.Text.Json.JsonElement left,
        System.Text.Json.JsonElement right)
    {
        var leftIds = EnumerateReferenceIds(left).ToHashSet();
        return leftIds.Count > 0
            && EnumerateReferenceIds(right).Any(leftIds.Contains);
    }

    private static IEnumerable<long> EnumerateReferenceIds(
        System.Text.Json.JsonElement element)
    {
        if (element.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
            {
                if (TryReadReferenceId(child, out var childId))
                {
                    yield return childId;
                }
            }

            yield break;
        }

        if (TryReadReferenceId(element, out var id))
        {
            yield return id;
        }
    }

    private static bool TryReadReferenceId(
        System.Text.Json.JsonElement element,
        out long id)
    {
        if (element.ValueKind == System.Text.Json.JsonValueKind.Number)
        {
            return element.TryGetInt64(out id);
        }

        id = 0;
        return element.ValueKind == System.Text.Json.JsonValueKind.String
            && long.TryParse(
                element.GetString(),
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out id);
    }

    private static bool IsHierarchyReference(ContentFieldDefinition field) =>
        string.Equals(
            GetStringSetting(field, ReferenceContentFieldSettings.SelectionMode),
            ReferenceContentFieldSettings.SelectionModeHierarchy,
            StringComparison.Ordinal);

    private static bool IsTrue(ContentFieldDefinition field, string key) =>
        field.Settings.TryGetValue(key, out var value)
        && value.ValueKind == System.Text.Json.JsonValueKind.True;

    private static string? GetStringSetting(ContentFieldDefinition field, string key) =>
        field.Settings.TryGetValue(key, out var value)
        && value.ValueKind == System.Text.Json.JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool TryGetTargetContentTypeId(ContentFieldDefinition field, out long id)
    {
        id = 0;
        return field.Settings.TryGetValue(ReferenceContentFieldSettings.TargetContentTypeId, out var value)
               && value.ValueKind == System.Text.Json.JsonValueKind.String
               && long.TryParse(value.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out id)
               && id > 0;
    }
}
