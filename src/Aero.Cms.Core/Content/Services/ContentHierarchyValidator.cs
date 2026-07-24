using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Enums;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;

namespace Aero.Cms.Core.Content.Services;

/// <summary>
/// Enforces content cardinality and hierarchy invariants before draft or published
/// content is persisted.
/// </summary>
public sealed class ContentHierarchyValidator(
    IDocumentSession session,
    IContentTypeService contentTypeService)
{
    /// <summary>The hard system depth limit applied in addition to type rules.</summary>
    public const int MaximumSystemDepth = 32;

    /// <summary>Maximum existing descendants inspected while validating a move.</summary>
    public const int MaximumSubtreeValidationItems = 5_000;

    /// <summary>Validates one content item's placement under its resolved type.</summary>
    public async Task<Result<ContentItem>> ValidateAsync(
        ContentItem item,
        ContentTypeDefinition contentType,
        ContentValidationMode mode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(contentType);

        if (item.SortOrder < 0)
        {
            return AeroError.ValidationError(["Content item sort order cannot be negative."]);
        }

        if (contentType.Cardinality == ContentCardinality.Singleton)
        {
            var hasAnother = await session.Query<ContentItem>()
                .Where(candidate =>
                    candidate.SiteId == item.SiteId
                    && candidate.ContentTypeAlias == item.ContentTypeAlias
                    && candidate.Culture == item.Culture
                    && candidate.Id != item.Id)
                .AnyAsync(cancellationToken);
            if (hasAnother)
            {
                return AeroError.ValidationError(
                    [$"Content type '{contentType.Alias}' permits only one item per culture."]);
            }
        }

        if (contentType.Structure == ContentStructure.Flat)
        {
            return item.ParentId is null
                ? new Result<ContentItem>.Ok(item)
                : new Result<ContentItem>.Failure(
                    AeroError.ValidationError(
                        [$"Content type '{contentType.Alias}' does not permit parent items."]));
        }

        var rules = contentType.HierarchyRules ?? new ContentHierarchyRules();
        if (rules.MaximumDepth is < 1 or > MaximumSystemDepth)
        {
            return AeroError.ValidationError(
                [$"Hierarchy maximum depth must be between 1 and {MaximumSystemDepth}."]);
        }

        if (item.ParentId is null)
        {
            if (!rules.AllowRootItems)
            {
                return AeroError.ValidationError(
                    [$"Content type '{contentType.Alias}' does not permit root items."]);
            }

            return await ValidateSubtreeDepthAsync(
                item,
                ancestorDepth: 0,
                rules.MaximumDepth,
                cancellationToken);
        }

        if (item.Id != 0 && item.ParentId == item.Id)
        {
            return AeroError.ValidationError(["A content item cannot be its own parent."]);
        }

        var visited = new HashSet<long>();
        if (item.Id != 0)
        {
            visited.Add(item.Id);
        }

        var parentId = item.ParentId;
        var depth = 0;
        while (parentId is { } currentParentId)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!visited.Add(currentParentId))
            {
                return AeroError.ValidationError(["The requested content hierarchy contains a cycle."]);
            }

            var parent = await session.LoadAsync<ContentItem>(currentParentId, cancellationToken);
            if (parent is null
                || parent.SiteId != item.SiteId
                || !string.Equals(parent.Culture, item.Culture, StringComparison.OrdinalIgnoreCase))
            {
                return AeroError.ValidationError(
                    ["The selected parent does not belong to the current site and culture."]);
            }

            depth++;
            if (depth > rules.MaximumDepth || depth > MaximumSystemDepth)
            {
                return AeroError.ValidationError(
                    [$"The content hierarchy cannot exceed depth {rules.MaximumDepth}."]);
            }

            if (depth == 1)
            {
                var parentTypeValidation = await ValidateParentTypeAsync(
                    item,
                    parent,
                    rules,
                    cancellationToken);
                if (parentTypeValidation is Result<bool>.Failure parentTypeFailure)
                {
                    return parentTypeFailure.Error;
                }
            }

            if (mode == ContentValidationMode.Publish
                && parent.PublicationState != ContentPublicationState.Published)
            {
                return AeroError.ValidationError(
                    ["A published hierarchical content item must have a published parent."]);
            }

            parentId = parent.ParentId;
        }

        return await ValidateSubtreeDepthAsync(
            item,
            depth,
            rules.MaximumDepth,
            cancellationToken);
    }

    private async Task<Result<ContentItem>> ValidateSubtreeDepthAsync(
        ContentItem item,
        int ancestorDepth,
        int maximumDepth,
        CancellationToken cancellationToken)
    {
        if (item.Id == 0)
        {
            return item;
        }

        var queue = new Queue<(long ParentId, int RelativeDepth)>();
        queue.Enqueue((item.Id, 0));
        var visited = new HashSet<long> { item.Id };
        var inspected = 0;

        while (queue.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (parentId, relativeDepth) = queue.Dequeue();
            var remaining = MaximumSubtreeValidationItems - inspected;
            if (remaining <= 0)
            {
                return AeroError.ValidationError(
                    [$"The content subtree exceeds the {MaximumSubtreeValidationItems}-item validation limit."]);
            }

            var children = await session.Query<ContentItem>()
                .Where(candidate => candidate.ParentId == parentId)
                .Take(remaining + 1)
                .ToListAsync(cancellationToken);
            if (children.Count > remaining)
            {
                return AeroError.ValidationError(
                    [$"The content subtree exceeds the {MaximumSubtreeValidationItems}-item validation limit."]);
            }

            foreach (var child in children)
            {
                inspected++;
                if (child.SiteId != item.SiteId
                    || !string.Equals(
                        child.Culture,
                        item.Culture,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return AeroError.ValidationError(
                        ["The existing content subtree crosses a site or culture boundary."]);
                }

                if (!visited.Add(child.Id))
                {
                    return AeroError.ValidationError(
                        ["The existing content subtree contains a cycle."]);
                }

                var childDepth = relativeDepth + 1;
                if (ancestorDepth + childDepth > maximumDepth
                    || ancestorDepth + childDepth > MaximumSystemDepth)
                {
                    return AeroError.ValidationError(
                        [$"The content hierarchy cannot exceed depth {maximumDepth}."]);
                }

                queue.Enqueue((child.Id, childDepth));
            }
        }

        return item;
    }

    private async Task<Result<bool>> ValidateParentTypeAsync(
        ContentItem item,
        ContentItem parent,
        ContentHierarchyRules rules,
        CancellationToken cancellationToken)
    {
        if (rules.RequireSameTypeParent)
        {
            return string.Equals(
                    item.ContentTypeAlias,
                    parent.ContentTypeAlias,
                    StringComparison.OrdinalIgnoreCase)
                ? new Result<bool>.Ok(true)
                : new Result<bool>.Failure(
                    AeroError.ValidationError(
                        ["The selected parent must use the same content type."]));
        }

        var parentTypeResult = await contentTypeService.GetByAliasAsync(
            item.SiteId,
            parent.ContentTypeAlias,
            cancellationToken);
        if (parentTypeResult is not Result<ContentTypeDefinition, AeroError>.Ok parentType)
        {
            return AeroError.ValidationError(["The selected parent content type was not found."]);
        }

        return rules.AllowedParentContentTypeIds.Contains(parentType.Value.Id)
            ? new Result<bool>.Ok(true)
            : new Result<bool>.Failure(
                AeroError.ValidationError(
                    [$"Content type '{parent.ContentTypeAlias}' is not an allowed parent type."]));
    }
}
