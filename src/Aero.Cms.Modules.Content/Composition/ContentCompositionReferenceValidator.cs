using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Content.Composition;
using Aero.Cms.Abstractions.Content.Views;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Pages.Composition;
using Aero.Cms.Core.Content.Services;
using Aero.Core;
using Aero.Core.Railway;
using System.Globalization;
using Aero.Core.Http;

namespace Aero.Cms.Modules.Content.Composition;

/// <summary>
/// Validates page-composition references through Content-owned, site-scoped services.
/// </summary>
public sealed class ContentCompositionReferenceValidator : IContentCompositionReferenceValidator
{
    private readonly IContentTypeService contentTypes;
    private readonly IContentService contentItems;
    private readonly IReadOnlyDictionary<string, IContentEntrySourceProvider> entryProviders;
    private readonly IContentEntrySourceProviderCatalog? entryProviderCatalog;
    private readonly ISiteContext? siteContext;

    public ContentCompositionReferenceValidator(
        IContentTypeService contentTypes,
        IContentService contentItems)
    {
        this.contentTypes = contentTypes;
        this.contentItems = contentItems;
        entryProviders = new Dictionary<string, IContentEntrySourceProvider>(StringComparer.OrdinalIgnoreCase);
    }

    public ContentCompositionReferenceValidator(
        IContentTypeService contentTypes,
        IContentService contentItems,
        IEnumerable<IContentEntrySourceProvider> entryProviders,
        ISiteContext siteContext)
        : this(contentTypes, contentItems, entryProviders, siteContext, null)
    {
    }

    public ContentCompositionReferenceValidator(
        IContentTypeService contentTypes,
        IContentService contentItems,
        IEnumerable<IContentEntrySourceProvider> entryProviders,
        ISiteContext siteContext,
        IContentEntrySourceProviderCatalog? entryProviderCatalog)
    {
        this.contentTypes = contentTypes;
        this.contentItems = contentItems;
        this.siteContext = siteContext;
        this.entryProviderCatalog = entryProviderCatalog;
        this.entryProviders = entryProviders
            .GroupBy(provider => provider.Provider, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public async Task<Result<bool, AeroError>> ValidateAsync(
        long siteId,
        string culture,
        PageCompositionDocument composition,
        ContentReferenceValidationMode mode,
        CancellationToken ct = default)
        => await ValidateAsync(
            siteContext is not null && siteContext.SiteId == siteId
                ? new ContentViewScope(siteContext.TenantId, siteId)
                : new ContentViewScope(0, siteId),
            culture,
            composition,
            mode,
            ct);

    /// <inheritdoc />
    public async Task<Result<bool, AeroError>> ValidateAsync(
        ContentViewScope scope,
        string culture,
        PageCompositionDocument composition,
        ContentReferenceValidationMode mode,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(composition);

        var siteId = scope.SiteId;

        if (siteId <= 0)
        {
            return Prelude.Fail<bool, AeroError>(
                AeroError.ValidationError(["A site is required to validate page content references."]));
        }

        var errors = new HashSet<string>(StringComparer.Ordinal);
        var lists = composition.ContentLists ?? [];
        var items = composition.ContentItems ?? [];
        var bindings = composition.FieldBindings ?? [];
        var queryDeclarations = composition.ContentQueries ?? [];
        if (queryDeclarations.Any(query => query is null))
        {
            errors.Add("Content query declarations cannot be null.");
        }

        var queries = queryDeclarations
            .Where(query => query is not null)
            .Select(query => query!)
            .ToArray();
        var persistedItems = items.Where(item => item.ContentEntryKey is null).ToArray();
        var virtualItems = items.Where(item => item.ContentEntryKey is not null).ToArray();
        var persistedLists = lists.Where(scope => string.IsNullOrWhiteSpace(scope.ContentEntryProvider)).ToArray();
        var scopes = persistedLists
            .Select(scope => (scope.NodeId, scope.ContentTypeId))
            .Concat(persistedItems.Select(scope => (scope.NodeId, scope.ContentTypeId)))
            .ToArray();
        var definitions = new Dictionary<long, ContentTypeDefinition>();

        var referencedTypeIds = scopes
            .Select(scope => scope.ContentTypeId)
            .Concat(queries.Select(query => query.ContentTypeId))
            .Distinct();
        foreach (var contentTypeId in referencedTypeIds)
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

        foreach (var list in persistedLists)
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

        foreach (var itemScope in persistedItems)
        {
            if (!definitions.TryGetValue(itemScope.ContentTypeId, out var definition))
            {
                continue;
            }

            var itemResult = itemScope.LookupMode switch
            {
                PageContentItemLookupMode.StableId when itemScope.ContentItemId is > 0 =>
                    await contentItems.LoadAsync(siteId, itemScope.ContentItemId.Value, ct),
                PageContentItemLookupMode.Slug when !string.IsNullOrWhiteSpace(itemScope.Slug) =>
                    await contentItems.GetBySlugAndTypeAsync(
                        siteId,
                        definition.Alias,
                        culture,
                        itemScope.Slug,
                        ct),
                _ => Prelude.Fail<ContentItem, AeroError>(
                    AeroError.ValidationError([$"Content item scope '{itemScope.NodeId}' has an invalid lookup."]))
            };

            if (itemResult is not Result<ContentItem, AeroError>.Ok item)
            {
                errors.Add($"Content item referenced by scope '{itemScope.NodeId}' no longer exists.");
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

        await ValidateVirtualItemsAsync(scope, virtualItems, errors, ct);
        await ValidateVirtualListsAsync(scope, lists.Where(list => !string.IsNullOrWhiteSpace(list.ContentEntryProvider)).ToArray(), errors, ct);

        var normalizedCulture = CultureInfo.GetCultureInfo(culture).Name;
        foreach (var query in queries)
        {
            if (!definitions.TryGetValue(query.ContentTypeId, out var definition))
            {
                continue;
            }

            if (definition.Structure != ContentStructure.Hierarchical)
            {
                errors.Add(
                    $"Content query '{query.Name}' requires hierarchical content type '{definition.Name}'.");
            }

            foreach (var fieldName in query.Projection.IsDefault ? [] : query.Projection)
            {
                ValidateQueryField(definition, fieldName, query.Name, errors);
            }

            if (query.Traversal is not (
                    ContentTraversal.Children
                    or ContentTraversal.Descendants
                    or ContentTraversal.Ancestors)
                || query.RootId is not > 0)
            {
                continue;
            }

            var rootResult = await contentItems.LoadAsync(siteId, query.RootId.Value, ct);
            if (rootResult is not Result<ContentItem, AeroError>.Ok root)
            {
                errors.Add(
                    $"Content root referenced by query '{query.Name}' no longer exists.");
                continue;
            }

            if (!string.Equals(
                    root.Value.ContentTypeAlias,
                    definition.Alias,
                    StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(
                    $"Content root '{root.Value.Id}' for query '{query.Name}' does not belong to content type '{definition.Name}'.");
            }

            if (root.Value.SiteId != siteId)
            {
                errors.Add(
                    $"Content root '{root.Value.Id}' for query '{query.Name}' does not belong to the current site.");
            }

            if (!string.Equals(
                    root.Value.Culture,
                    normalizedCulture,
                    StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(
                    $"Content root '{root.Value.Id}' for query '{query.Name}' does not belong to culture '{normalizedCulture}'.");
            }

            if (mode == ContentReferenceValidationMode.Publishing
                && root.Value.PublicationState != ContentPublicationState.Published)
            {
                errors.Add(
                    $"Content root '{root.Value.Id}' for query '{query.Name}' must be published before this page can be published.");
            }
        }

        return errors.Count == 0
            ? Prelude.Ok<bool, AeroError>(true)
            : Prelude.Fail<bool, AeroError>(
                AeroError.ValidationError(errors.OrderBy(error => error, StringComparer.Ordinal)));
    }

    private async Task ValidateVirtualItemsAsync(
        ContentViewScope scope,
        IReadOnlyList<PageContentItemScope> virtualItems,
        ISet<string> errors,
        CancellationToken ct)
    {
        if (virtualItems.Count == 0)
        {
            return;
        }

        if (!scope.IsValid)
        {
            errors.Add("A current tenant and site are required to validate virtual page content.");
            return;
        }

        foreach (var item in virtualItems)
        {
            var key = item.ContentEntryKey!.Value;
            var routeBound = !string.IsNullOrWhiteSpace(item.StableIdRouteParameter);
            if (string.IsNullOrWhiteSpace(key.Provider)
                || (!routeBound && string.IsNullOrWhiteSpace(key.StableId)))
            {
                errors.Add($"Virtual content entry referenced by scope '{item.NodeId}' has an invalid key.");
                continue;
            }

            var provider = entryProviders.GetValueOrDefault(key.Provider)
                ?? (entryProviderCatalog is null
                    ? null
                    : await entryProviderCatalog.ResolveAsync(scope, key.Provider, ct));
            if (provider is null)
            {
                errors.Add($"Virtual content provider '{key.Provider}' referenced by scope '{item.NodeId}' no longer exists.");
                continue;
            }

            if (routeBound)
            {
                continue;
            }

            var entry = await provider.FindAsync(scope, key.StableId, ct);
            if (entry is null
                || entry.Scope != scope
                || !string.Equals(entry.Key.Provider, key.Provider, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(entry.Key.StableId, key.StableId, StringComparison.Ordinal))
            {
                errors.Add($"Virtual content entry referenced by scope '{item.NodeId}' no longer exists for the current site.");
            }
        }
    }

    private async Task ValidateVirtualListsAsync(
        ContentViewScope scope,
        IReadOnlyList<PageContentListScope> virtualLists,
        ISet<string> errors,
        CancellationToken ct)
    {
        if (virtualLists.Count == 0) return;
        if (!scope.IsValid)
        {
            errors.Add("A current tenant and site are required to validate virtual page content.");
            return;
        }

        foreach (var list in virtualLists)
        {
            var providerKey = list.ContentEntryProvider!.Trim();
            if (providerKey.Length is 0 or > 128)
            {
                errors.Add($"Virtual content provider referenced by scope '{list.NodeId}' is invalid.");
                continue;
            }
            var provider = entryProviders.GetValueOrDefault(providerKey)
                ?? (entryProviderCatalog is null ? null : await entryProviderCatalog.ResolveAsync(scope, providerKey, ct));
            if (provider is null || !string.Equals(provider.Provider, providerKey, StringComparison.OrdinalIgnoreCase))
                errors.Add($"Virtual content provider '{providerKey}' referenced by scope '{list.NodeId}' no longer exists for the current site.");
        }
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

    private static void ValidateQueryField(
        ContentTypeDefinition definition,
        string fieldName,
        string queryName,
        ISet<string> errors)
    {
        if (!definition.Fields.Any(field =>
                string.Equals(field.Name, fieldName, StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add(
                $"Content query field '{fieldName}' in query '{queryName}' does not exist on content type '{definition.Name}'.");
        }
    }
}
