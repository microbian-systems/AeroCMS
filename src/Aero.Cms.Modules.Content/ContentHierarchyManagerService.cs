using System.Globalization;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Core;
using Aero.Cms.Core.Content;
using Aero.Cms.Core.Content.Services;
using Aero.Cms.Modules.Content.Caching;
using Aero.Core.Http;
using Aero.Core.Railway;
using AeroDB.Sable;

namespace Aero.Cms.Modules.Content;

/// <summary>
/// Reads and mutates the manager-facing content hierarchy through one bounded,
/// site- and culture-scoped Sable unit of work.
/// </summary>
internal sealed class ContentHierarchyManagerService(
    IDocumentSession session,
    IContentTypeService contentTypeService,
    ContentHierarchyValidator hierarchyValidator,
    ISiteContext siteContext,
    ContentCacheInvalidator cacheInvalidator,
    ILogger<ContentHierarchyManagerService> logger)
{
    /// <summary>Maximum documents materialized for one manager hierarchy.</summary>
    public const int MaximumHierarchyItems = 5_000;

    /// <summary>Gets a pre-shaped tree containing target items and every eligible parent context.</summary>
    public async Task<Result<ContentHierarchyTreeResult>> GetTreeAsync(
        string alias,
        string? culture,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedAlias = NormalizeAlias(alias);
            var normalizedCulture = NormalizeCulture(culture);
            var typeResult = await contentTypeService.GetByAliasAsync(
                siteContext.SiteId,
                normalizedAlias,
                cancellationToken);
            if (typeResult is not Result<ContentTypeDefinition, AeroError>.Ok typeOk)
            {
                return AeroError.NotFoundError("Content type or hierarchy was not found.");
            }

            if (typeOk.Value.Structure != ContentStructure.Hierarchical)
            {
                return AeroError.ValidationError(
                    [$"Content type '{normalizedAlias}' does not define a hierarchy."]);
            }

            var items = await session.Query<ContentItem>()
                .Where(item =>
                    item.SiteId == siteContext.SiteId
                    && item.Culture == normalizedCulture)
                .Take(MaximumHierarchyItems + 1)
                .ToListAsync(cancellationToken);
            if (items.Count > MaximumHierarchyItems)
            {
                return AeroError.ValidationError(
                    [$"The manager hierarchy exceeds the {MaximumHierarchyItems}-item limit."]);
            }

            var eligibleAliases = await ResolveEligibleParentAliasesAsync(
                typeOk.Value,
                cancellationToken);

            return BuildTree(normalizedAlias, normalizedCulture, items, eligibleAliases);
        }
        catch (CultureNotFoundException)
        {
            return AeroError.ValidationError(["The requested content culture is invalid."]);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to load content hierarchy {ContentTypeAlias} for site {SiteId}",
                alias,
                siteContext.SiteId);
            return AeroError.DatabaseError("Failed to load the content hierarchy.");
        }
    }

    /// <summary>
    /// Moves one item and normalizes old and new sibling orders before one Sable commit.
    /// </summary>
    public async Task<Result<ContentHierarchyTreeResult>> MoveAsync(
        string alias,
        long itemId,
        MoveContentItemRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.TargetIndex < 0)
        {
            return AeroError.ValidationError(["The target sibling position cannot be negative."]);
        }

        try
        {
            var normalizedAlias = NormalizeAlias(alias);
            var item = await session.LoadAsync<ContentItem>(itemId, cancellationToken);
            if (item is null
                || item.SiteId != siteContext.SiteId
                || !string.Equals(
                    item.ContentTypeAlias,
                    normalizedAlias,
                    StringComparison.OrdinalIgnoreCase))
            {
                return AeroError.NotFoundError("Content item or hierarchy was not found.");
            }

            var normalizedCulture = NormalizeCulture(request.Culture ?? item.Culture);
            if (!string.Equals(item.Culture, normalizedCulture, StringComparison.OrdinalIgnoreCase))
            {
                return AeroError.NotFoundError("Content item or hierarchy was not found.");
            }

            var typeResult = await contentTypeService.GetByAliasAsync(
                siteContext.SiteId,
                item.ContentTypeAlias,
                cancellationToken);
            if (typeResult is not Result<ContentTypeDefinition, AeroError>.Ok typeOk)
            {
                return AeroError.NotFoundError("Content type or hierarchy was not found.");
            }

            var oldParentId = item.ParentId;
            item.ParentId = request.NewParentId;
            var validation = await hierarchyValidator.ValidateAsync(
                item,
                typeOk.Value,
                ContentValidationMode.Draft,
                cancellationToken);
            if (validation is Result<ContentItem>.Failure validationFailure)
            {
                item.ParentId = oldParentId;
                return validationFailure.Error;
            }

            var changed = new Dictionary<long, ContentItem>();
            if (oldParentId == request.NewParentId)
            {
                var siblings = await LoadSiblingsAsync(
                    item.SiteId,
                    item.Culture,
                    oldParentId,
                    item.ContentTypeAlias,
                    cancellationToken);
                InsertAt(siblings, item, request.TargetIndex);
                NormalizeSiblingOrder(siblings, changed);
            }
            else
            {
                var oldSiblings = await LoadSiblingsAsync(
                    item.SiteId,
                    item.Culture,
                    oldParentId,
                    item.ContentTypeAlias,
                    cancellationToken);
                oldSiblings.RemoveAll(sibling => sibling.Id == item.Id);
                NormalizeSiblingOrder(oldSiblings, changed);

                var newSiblings = await LoadSiblingsAsync(
                    item.SiteId,
                    item.Culture,
                    request.NewParentId,
                    item.ContentTypeAlias,
                    cancellationToken);
                InsertAt(newSiblings, item, request.TargetIndex);
                NormalizeSiblingOrder(newSiblings, changed);
            }

            foreach (var changedItem in changed.Values)
            {
                session.Store(changedItem);
            }

            await session.SaveChangesAsync(cancellationToken);
            await InvalidateAsync(changed.Values);

            return await GetTreeAsync(normalizedAlias, normalizedCulture, cancellationToken);
        }
        catch (CultureNotFoundException)
        {
            return AeroError.ValidationError(["The requested content culture is invalid."]);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to move content item {ContentItemId} for site {SiteId}",
                itemId,
                siteContext.SiteId);
            return AeroError.DatabaseError("Failed to move the content item.");
        }
    }

    /// <summary>Replaces one exact sibling order and commits every change atomically.</summary>
    public async Task<Result<ContentHierarchyTreeResult>> ReorderAsync(
        string alias,
        ReorderContentSiblingsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            var normalizedAlias = NormalizeAlias(alias);
            var normalizedCulture = NormalizeCulture(request.Culture);
            var typeResult = await contentTypeService.GetByAliasAsync(
                siteContext.SiteId,
                normalizedAlias,
                cancellationToken);
            if (typeResult is not Result<ContentTypeDefinition, AeroError>.Ok typeOk
                || typeOk.Value.Structure != ContentStructure.Hierarchical)
            {
                return AeroError.NotFoundError("Content type or hierarchy was not found.");
            }

            var siblings = await LoadSiblingsAsync(
                siteContext.SiteId,
                normalizedCulture,
                request.ParentId,
                normalizedAlias,
                cancellationToken);

            if (request.OrderedIds.Count != siblings.Count
                || request.OrderedIds.Distinct().Count() != request.OrderedIds.Count
                || !request.OrderedIds.ToHashSet().SetEquals(siblings.Select(item => item.Id)))
            {
                return AeroError.ConflictError(
                    "The sibling collection changed. Reload the hierarchy and try again.");
            }

            var byId = siblings.ToDictionary(item => item.Id);
            var changed = new Dictionary<long, ContentItem>();
            NormalizeSiblingOrder(
                request.OrderedIds.Select(id => byId[id]).ToList(),
                changed);

            foreach (var item in changed.Values)
            {
                session.Store(item);
            }

            await session.SaveChangesAsync(cancellationToken);
            await InvalidateAsync(changed.Values);
            return await GetTreeAsync(normalizedAlias, normalizedCulture, cancellationToken);
        }
        catch (CultureNotFoundException)
        {
            return AeroError.ValidationError(["The requested content culture is invalid."]);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to reorder content hierarchy {ContentTypeAlias} for site {SiteId}",
                alias,
                siteContext.SiteId);
            return AeroError.DatabaseError("Failed to reorder content items.");
        }
    }

    private async Task<HashSet<string>> ResolveEligibleParentAliasesAsync(
        ContentTypeDefinition targetType,
        CancellationToken cancellationToken)
    {
        var rules = targetType.HierarchyRules ?? new ContentHierarchyRules();
        if (rules.RequireSameTypeParent)
        {
            return new HashSet<string>([targetType.Alias], StringComparer.OrdinalIgnoreCase);
        }

        var allowedIds = rules.AllowedParentContentTypeIds.ToHashSet();
        if (allowedIds.Count == 0)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var documents = await session.Query<ContentTypeDocument>()
            .Where(document => document.SiteId == siteContext.SiteId)
            .ToListAsync(cancellationToken);
        return documents
            .Where(document => allowedIds.Contains(document.Id))
            .Select(document => document.Alias)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private async Task<List<ContentItem>> LoadSiblingsAsync(
        long siteId,
        string culture,
        long? parentId,
        string contentTypeAlias,
        CancellationToken cancellationToken)
    {
        var siblings = await session.Query<ContentItem>()
            .Where(item =>
                item.SiteId == siteId
                && item.Culture == culture
                && item.ParentId == parentId
                && item.ContentTypeAlias == contentTypeAlias)
            .Take(MaximumHierarchyItems + 1)
            .ToListAsync(cancellationToken);
        if (siblings.Count > MaximumHierarchyItems)
        {
            throw new InvalidOperationException(
                $"The sibling collection exceeds {MaximumHierarchyItems} items.");
        }

        return siblings
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void InsertAt(List<ContentItem> siblings, ContentItem item, int targetIndex)
    {
        siblings.RemoveAll(sibling => sibling.Id == item.Id);
        siblings.Insert(Math.Clamp(targetIndex, 0, siblings.Count), item);
    }

    private static void NormalizeSiblingOrder(
        IReadOnlyList<ContentItem> siblings,
        IDictionary<long, ContentItem> changed)
    {
        for (var index = 0; index < siblings.Count; index++)
        {
            var item = siblings[index];
            if (item.SortOrder != index)
            {
                item.SortOrder = index;
            }

            changed[item.Id] = item;
        }
    }

    private async Task InvalidateAsync(IEnumerable<ContentItem> items)
    {
        foreach (var item in items)
        {
            var identity = new ContentItemCacheIdentity(
                item.SiteId,
                item.Id,
                item.ContentTypeAlias,
                item.Culture,
                item.Slug,
                item.TranslationGroupId ?? item.Id);
            await cacheInvalidator.InvalidateItemAsync(identity, identity);
        }
    }

    private static Result<ContentHierarchyTreeResult> BuildTree(
        string targetAlias,
        string culture,
        IReadOnlyList<ContentItem> allItems,
        IReadOnlySet<string> eligibleParentAliases)
    {
        var byId = allItems.ToDictionary(item => item.Id);
        var includedIds = new HashSet<long>();
        foreach (var item in allItems.Where(item =>
                     string.Equals(item.ContentTypeAlias, targetAlias, StringComparison.OrdinalIgnoreCase)
                     || eligibleParentAliases.Contains(item.ContentTypeAlias)))
        {
            var current = item;
            while (includedIds.Add(current.Id)
                   && current.ParentId is { } parentId
                   && byId.TryGetValue(parentId, out var parent))
            {
                current = parent;
            }
        }

        var included = allItems.Where(item => includedIds.Contains(item.Id)).ToArray();
        var childrenByParent = included
            .Where(item => item.ParentId.HasValue)
            .GroupBy(item => item.ParentId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(item => item.SortOrder)
                    .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
                    .ToArray());
        var recursionStack = new HashSet<long>();

        Result<ContentHierarchyTreeNode> BuildNode(ContentItem item, int depth)
        {
            if (!recursionStack.Add(item.Id))
            {
                return AeroError.ValidationError(["The stored content hierarchy contains a cycle."]);
            }

            var children = new List<ContentHierarchyTreeNode>();
            if (childrenByParent.TryGetValue(item.Id, out var childItems))
            {
                foreach (var child in childItems)
                {
                    var childResult = BuildNode(child, depth + 1);
                    if (childResult is Result<ContentHierarchyTreeNode>.Failure failure)
                    {
                        return failure.Error;
                    }

                    children.Add(((Result<ContentHierarchyTreeNode>.Ok)childResult).Value);
                }
            }

            recursionStack.Remove(item.Id);
            return new ContentHierarchyTreeNode(
                item.Id,
                item.Title,
                item.Slug,
                item.ContentTypeAlias,
                item.Culture,
                item.PublicationState.ToString(),
                item.ParentId,
                item.SortOrder,
                depth,
                string.Equals(item.ContentTypeAlias, targetAlias, StringComparison.OrdinalIgnoreCase),
                eligibleParentAliases.Contains(item.ContentTypeAlias),
                children);
        }

        var rootItems = included
            .Where(item => item.ParentId is null || !includedIds.Contains(item.ParentId.Value))
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var roots = new List<ContentHierarchyTreeNode>();
        foreach (var root in rootItems)
        {
            var rootResult = BuildNode(root, 0);
            if (rootResult is Result<ContentHierarchyTreeNode>.Failure failure)
            {
                return failure.Error;
            }

            roots.Add(((Result<ContentHierarchyTreeNode>.Ok)rootResult).Value);
        }

        if (roots.Sum(CountNodes) != included.Length)
        {
            return AeroError.ValidationError(
                ["The stored content hierarchy has no valid root because it contains a cycle."]);
        }

        return new ContentHierarchyTreeResult(
            targetAlias,
            culture,
            allItems.Count(item =>
                string.Equals(item.ContentTypeAlias, targetAlias, StringComparison.OrdinalIgnoreCase)),
            roots);
    }

    private static int CountNodes(ContentHierarchyTreeNode node)
        => 1 + node.Children.Sum(CountNodes);

    private static string NormalizeAlias(string alias)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);
        return alias.Trim().ToLowerInvariant();
    }

    private static string NormalizeCulture(string? culture)
        => CultureInfo.GetCultureInfo(
            string.IsNullOrWhiteSpace(culture)
                ? CultureInfo.CurrentUICulture.Name
                : culture.Trim()).Name;
}
