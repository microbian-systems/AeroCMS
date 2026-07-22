using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Content.Composition;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Pages.Composition;
using Aero.Cms.Core.Content.Services;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Modules.Content.Composition;

/// <summary>
/// Validates page-composition references through Content-owned, site-scoped services.
/// </summary>
public sealed class ContentCompositionReferenceValidator(
    IContentTypeService contentTypes,
    IContentService contentItems) : IContentCompositionReferenceValidator
{
    /// <inheritdoc />
    public async Task<Result<bool, AeroError>> ValidateAsync(
        long siteId,
        string culture,
        PageCompositionDocument composition,
        ContentReferenceValidationMode mode,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(composition);

        if (siteId <= 0)
        {
            return Prelude.Fail<bool, AeroError>(
                AeroError.ValidationError(["A site is required to validate page content references."]));
        }

        var errors = new HashSet<string>(StringComparer.Ordinal);
        var lists = composition.ContentLists ?? [];
        var items = composition.ContentItems ?? [];
        var bindings = composition.FieldBindings ?? [];
        var scopes = lists
            .Select(scope => (scope.NodeId, scope.ContentTypeId))
            .Concat(items.Select(scope => (scope.NodeId, scope.ContentTypeId)))
            .ToArray();
        var definitions = new Dictionary<long, ContentTypeDefinition>();

        foreach (var contentTypeId in scopes.Select(scope => scope.ContentTypeId).Distinct())
        {
            var result = await contentTypes.GetByIdAsync(siteId, contentTypeId, ct);
            if (result is Result<ContentTypeDefinition, AeroError>.Ok ok)
            {
                definitions[contentTypeId] = ok.Value;
            }
            else
            {
                errors.Add($"Content type '{contentTypeId}' referenced by this page no longer exists.");
            }
        }

        foreach (var list in lists)
        {
            if (!definitions.TryGetValue(list.ContentTypeId, out var definition))
            {
                continue;
            }

            ValidateField(definition, list.Query.SortField, "sort", list.NodeId, errors);
            foreach (var filter in list.Query.Filters ?? [])
            {
                ValidateField(definition, filter.FieldName, "filter", list.NodeId, errors);
            }
        }

        var scopeTypeIds = scopes
            .GroupBy(scope => scope.NodeId)
            .ToDictionary(group => group.Key, group => group.First().ContentTypeId);
        foreach (var binding in bindings)
        {
            if (scopeTypeIds.TryGetValue(binding.ScopeNodeId, out var contentTypeId)
                && definitions.TryGetValue(contentTypeId, out var definition))
            {
                ValidateField(definition, binding.FieldName, "binding", binding.ScopeNodeId, errors);
            }
        }

        foreach (var scope in items)
        {
            if (!definitions.TryGetValue(scope.ContentTypeId, out var definition))
            {
                continue;
            }

            var itemResult = scope.LookupMode switch
            {
                PageContentItemLookupMode.StableId when scope.ContentItemId is > 0 =>
                    await contentItems.LoadAsync(siteId, scope.ContentItemId.Value, ct),
                PageContentItemLookupMode.Slug when !string.IsNullOrWhiteSpace(scope.Slug) =>
                    await contentItems.GetBySlugAndTypeAsync(
                        siteId,
                        definition.Alias,
                        culture,
                        scope.Slug,
                        ct),
                _ => Prelude.Fail<ContentItem, AeroError>(
                    AeroError.ValidationError([$"Content item scope '{scope.NodeId}' has an invalid lookup."]))
            };

            if (itemResult is not Result<ContentItem, AeroError>.Ok item)
            {
                errors.Add($"Content item referenced by scope '{scope.NodeId}' no longer exists.");
                continue;
            }

            if (!string.Equals(
                    item.Value.ContentTypeAlias,
                    definition.Alias,
                    StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(
                    $"Content item '{item.Value.Id}' does not belong to content type '{definition.Name}'.");
            }

            if (mode == ContentReferenceValidationMode.Publishing
                && item.Value.PublicationState != ContentPublicationState.Published)
            {
                errors.Add($"Content item '{item.Value.Id}' must be published before this page can be published.");
            }
        }

        return errors.Count == 0
            ? Prelude.Ok<bool, AeroError>(true)
            : Prelude.Fail<bool, AeroError>(
                AeroError.ValidationError(errors.OrderBy(error => error, StringComparer.Ordinal)));
    }

    private static void ValidateField(
        ContentTypeDefinition definition,
        string? fieldName,
        string referenceKind,
        long scopeNodeId,
        ISet<string> errors)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            return;
        }

        if (!definition.Fields.Any(field =>
                string.Equals(field.Name, fieldName, StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add(
                $"Content {referenceKind} field '{fieldName}' in scope '{scopeNodeId}' does not exist on content type '{definition.Name}'.");
        }
    }
}
