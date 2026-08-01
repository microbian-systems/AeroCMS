using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Enums;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;

namespace Aero.Cms.Core.Content.Services;

/// <summary>
/// Builds immutable bounded hierarchy projections from traversal-specific Sable
/// queries. Renderers receive only completed results and never the session.
/// </summary>
public sealed class ContentHierarchyQueryService(
    IDocumentSession session,
    IContentTypeService contentTypeService) : IContentHierarchyQueryService
{
    /// <summary>Hard maximum number of nodes returned to a renderer.</summary>
    public const int MaximumItems = 500;

    /// <summary>Hard maximum hierarchy depth returned to a renderer.</summary>
    public const int MaximumDepth = 16;

    /// <summary>Hard approximate UTF-8 output-size budget.</summary>
    public const int MaximumOutputBytes = 1024 * 1024;

    /// <inheritdoc />
    public async Task<Result<ContentQueryResult>> QueryAsync(
        ContentQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var validationError = ValidateRequest(request);
        if (validationError is not null)
        {
            return AeroError.ValidationError([validationError]);
        }

        var culture = CultureInfo.GetCultureInfo(request.Culture).Name;
        var typeResult = await contentTypeService.GetByIdAsync(
            request.SiteId,
            request.ContentTypeId,
            cancellationToken);
        if (typeResult is not Result<ContentTypeDefinition, AeroError>.Ok typeSuccess)
        {
            return AeroError.NotFoundError(
                $"Content type '{request.ContentTypeId}' was not found.");
        }

        var type = typeSuccess.Value;
        if (type.Structure != ContentStructure.Hierarchical)
        {
            return AeroError.ValidationError(
                [$"Content type '{type.Alias}' is not hierarchical."]);
        }

        var projectionResult = ResolveProjection(type, request.Projection);
        if (projectionResult is Result<IReadOnlySet<string>>.Failure projectionFailure)
        {
            return projectionFailure.Error;
        }

        var projection = ((Result<IReadOnlySet<string>>.Ok)projectionResult).Value;
        var requestedLimit = Math.Min(request.MaximumItems, MaximumItems);
        var requestedDepth = Math.Min(request.MaximumDepth, MaximumDepth);

        Result<HierarchyProjection> projectionResultValue;
        if (request.Traversal == ContentTraversal.Ancestors)
        {
            projectionResultValue = await BuildAncestorsAsync(
                request,
                type.Alias,
                culture,
                projection,
                requestedLimit,
                requestedDepth,
                cancellationToken);
        }
        else
        {
            projectionResultValue = await BuildDownwardAsync(
                request,
                type.Alias,
                culture,
                projection,
                requestedLimit,
                requestedDepth,
                cancellationToken);
        }

        if (projectionResultValue is Result<HierarchyProjection>.Failure hierarchyFailure)
        {
            return hierarchyFailure.Error;
        }

        var hierarchy = ((Result<HierarchyProjection>.Ok)projectionResultValue).Value;
        var result = new ContentQueryResult(
            request.Name.Trim(),
            type.Alias,
            hierarchy.Nodes,
            CountNodes(hierarchy.Nodes),
            hierarchy.WasTruncated);
        if (EstimateUtf8Bytes(result) > MaximumOutputBytes)
        {
            return AeroError.ValidationError(
                ["The content hierarchy result exceeded the output-size limit."]);
        }

        return result;
    }

    private async Task<Result<HierarchyProjection>> BuildDownwardAsync(
        ContentQueryRequest request,
        string contentTypeAlias,
        string culture,
        IReadOnlySet<string> projection,
        int maximumItems,
        int maximumDepth,
        CancellationToken cancellationToken)
    {
        if (request.Traversal is ContentTraversal.Children or ContentTraversal.Descendants)
        {
            var rootResult = await LoadRequiredRootAsync(
                request,
                contentTypeAlias,
                culture,
                cancellationToken);
            if (rootResult is Result<ContentItem>.Failure rootFailure)
            {
                return rootFailure.Error;
            }
        }

        var startingItems = request.Traversal switch
        {
            ContentTraversal.Roots or ContentTraversal.RootsWithDescendants =>
                await LoadRootsAsync(
                    request,
                    contentTypeAlias,
                    culture,
                    maximumItems + 1,
                    cancellationToken),
            ContentTraversal.Children or ContentTraversal.Descendants =>
                await LoadChildrenAsync(
                    request,
                    contentTypeAlias,
                    culture,
                    request.RootId!.Value,
                    maximumItems + 1,
                    cancellationToken),
            _ => []
        };

        var state = new DownwardProjectionState(
            this,
            request,
            contentTypeAlias,
            culture,
            projection,
            maximumItems,
            maximumDepth,
            startingItems.Count > maximumItems);
        var includeChildren = request.Traversal is
            ContentTraversal.Descendants or ContentTraversal.RootsWithDescendants;
        return await state.ProjectAsync(
            startingItems.Take(maximumItems).ToArray(),
            includeChildren,
            cancellationToken);
    }

    private async Task<Result<HierarchyProjection>> BuildAncestorsAsync(
        ContentQueryRequest request,
        string contentTypeAlias,
        string culture,
        IReadOnlySet<string> projection,
        int maximumItems,
        int maximumDepth,
        CancellationToken cancellationToken)
    {
        var rootResult = await LoadRequiredRootAsync(
            request,
            contentTypeAlias,
            culture,
            cancellationToken);
        if (rootResult is Result<ContentItem>.Failure rootFailure)
        {
            return rootFailure.Error;
        }

        var current = ((Result<ContentItem>.Ok)rootResult).Value;
        var ancestors = new List<ContentItem>();
        var visited = new HashSet<long> { current.Id };
        var parentId = current.ParentId;
        var maximumAncestors = Math.Min(maximumItems, maximumDepth);
        while (parentId is { } currentParentId && ancestors.Count < maximumAncestors)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!visited.Add(currentParentId))
            {
                return AeroError.ValidationError(
                    ["The persisted content hierarchy contains a cycle."]);
            }

            var parent = await session.LoadAsync<ContentItem>(
                currentParentId,
                cancellationToken);
            if (!IsAllowed(parent, request, contentTypeAlias, culture))
            {
                return AeroError.ValidationError(
                    ["The persisted content hierarchy crosses a site, culture, type, or publication boundary."]);
            }

            ancestors.Add(parent!);
            parentId = parent!.ParentId;
        }

        ancestors.Reverse();
        return new HierarchyProjection(
            ancestors.Select(item => ProjectLeaf(item, projection)).ToImmutableArray(),
            parentId is not null);
    }

    private async Task<Result<ContentItem>> LoadRequiredRootAsync(
        ContentQueryRequest request,
        string contentTypeAlias,
        string culture,
        CancellationToken cancellationToken)
    {
        if (request.RootId is not { } rootId)
        {
            return AeroError.ValidationError(
                ["The selected hierarchy traversal requires a root item ID."]);
        }

        var root = await session.LoadAsync<ContentItem>(rootId, cancellationToken);
        return IsAllowed(root, request, contentTypeAlias, culture)
            ? root!
            : AeroError.NotFoundError("The requested hierarchy root was not found.");
    }

    private async Task<IReadOnlyList<ContentItem>> LoadRootsAsync(
        ContentQueryRequest request,
        string contentTypeAlias,
        string culture,
        int take,
        CancellationToken cancellationToken)
    {
        var query = session.Query<ContentItem>()
            .Where(item =>
                item.SiteId == request.SiteId
                && item.ContentTypeAlias == contentTypeAlias
                && item.Culture == culture
                && item.ParentId == null);
        if (!request.IncludeDrafts)
        {
            query = query.Where(item =>
                item.PublicationState == ContentPublicationState.Published);
        }

        return await query
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Title)
            .Take(Math.Min(take, MaximumItems + 1))
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<ContentItem>> LoadChildrenAsync(
        ContentQueryRequest request,
        string contentTypeAlias,
        string culture,
        long parentId,
        int take,
        CancellationToken cancellationToken)
    {
        var query = session.Query<ContentItem>()
            .Where(item =>
                item.SiteId == request.SiteId
                && item.ContentTypeAlias == contentTypeAlias
                && item.Culture == culture
                && item.ParentId == parentId);
        if (!request.IncludeDrafts)
        {
            query = query.Where(item =>
                item.PublicationState == ContentPublicationState.Published);
        }

        return await query
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Title)
            .Take(Math.Clamp(take, 1, MaximumItems + 1))
            .ToListAsync(cancellationToken);
    }

    private static Result<IReadOnlySet<string>> ResolveProjection(
        ContentTypeDefinition type,
        ImmutableArray<string> requested)
    {
        var declared = type.Fields
            .Select(field => field.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (requested.IsDefaultOrEmpty)
        {
            return new Result<IReadOnlySet<string>>.Ok(declared);
        }

        var projection = requested
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unknown = projection.Where(name => !declared.Contains(name)).ToArray();
        return unknown.Length == 0
            ? new Result<IReadOnlySet<string>>.Ok(projection)
            : new Result<IReadOnlySet<string>>.Failure(
                AeroError.ValidationError(
                    unknown.Select(name =>
                        $"Content field '{name}' is not declared by '{type.Alias}'.")));
    }

    private static string? ValidateRequest(ContentQueryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 64)
            return "A content query requires a binding name of at most 64 characters.";
        if (request.SiteId <= 0)
            return "A content query requires a current site.";
        if (request.ContentTypeId <= 0)
            return "A content query requires a stable content type.";
        if (!Enum.IsDefined(request.Traversal))
            return "The content traversal is invalid.";
        if (request.Traversal is (
                ContentTraversal.Children
                or ContentTraversal.Descendants
                or ContentTraversal.Ancestors)
            && request.RootId is not > 0)
        {
            return "The selected hierarchy traversal requires a root item ID.";
        }
        if (request.Traversal is (
                ContentTraversal.Roots
                or ContentTraversal.RootsWithDescendants)
            && request.RootId is not null)
        {
            return "Root hierarchy traversals cannot specify a root item ID.";
        }

        if (request.MaximumDepth is < 1 or > MaximumDepth)
            return $"Content query depth must be between 1 and {MaximumDepth}.";
        if (request.MaximumItems is < 1 or > MaximumItems)
            return $"Content query item count must be between 1 and {MaximumItems}.";

        try
        {
            _ = CultureInfo.GetCultureInfo(request.Culture);
        }
        catch (CultureNotFoundException)
        {
            return "The content query culture is invalid.";
        }

        return null;
    }

    private static bool IsAllowed(
        ContentItem? item,
        ContentQueryRequest request,
        string contentTypeAlias,
        string culture)
        => item is not null
           && item.SiteId == request.SiteId
           && string.Equals(
               item.ContentTypeAlias,
               contentTypeAlias,
               StringComparison.OrdinalIgnoreCase)
           && string.Equals(item.Culture, culture, StringComparison.OrdinalIgnoreCase)
           && (request.IncludeDrafts
               || item.PublicationState == ContentPublicationState.Published);

    private static ContentNode ProjectLeaf(
        ContentItem item,
        IReadOnlySet<string> projection)
        => new(
            item.Id.ToString(CultureInfo.InvariantCulture),
            item.ContentTypeAlias,
            item.Title ?? string.Empty,
            item.Slug,
            ProjectFields(item, projection),
            []);

    private static ImmutableDictionary<string, System.Text.Json.JsonElement> ProjectFields(
        ContentItem item,
        IReadOnlySet<string> projection)
        => item.Fields
            .Where(field => projection.Contains(field.Key))
            .ToImmutableDictionary(
                field => field.Key,
                field => field.Value.Clone(),
                StringComparer.OrdinalIgnoreCase);

    private static int CountNodes(IEnumerable<ContentNode> nodes)
        => nodes.Sum(node => 1 + CountNodes(node.Children));

    private static int EstimateUtf8Bytes(ContentQueryResult result)
    {
        var bytes = Encoding.UTF8.GetByteCount(result.Name);
        foreach (var root in result.Roots)
        {
            bytes += EstimateUtf8Bytes(root);
        }

        return bytes;
    }

    private static int EstimateUtf8Bytes(ContentNode node)
    {
        var bytes = Encoding.UTF8.GetByteCount(
            node.Id + node.ContentType + node.Title + node.Slug);
        foreach (var field in node.Fields)
        {
            bytes += Encoding.UTF8.GetByteCount(field.Key);
            bytes += Encoding.UTF8.GetByteCount(field.Value.GetRawText());
        }

        return bytes + node.Children.Sum(EstimateUtf8Bytes);
    }

    private sealed record HierarchyProjection(
        ImmutableArray<ContentNode> Nodes,
        bool WasTruncated);

    private sealed class DownwardProjectionState(
        ContentHierarchyQueryService owner,
        ContentQueryRequest request,
        string contentTypeAlias,
        string culture,
        IReadOnlySet<string> projection,
        int maximumItems,
        int maximumDepth,
        bool wasTruncated)
    {
        private int _emitted;

        private bool WasTruncated { get; set; } = wasTruncated;

        public async Task<Result<HierarchyProjection>> ProjectAsync(
            IReadOnlyList<ContentItem> startingItems,
            bool includeChildren,
            CancellationToken cancellationToken)
        {
            var roots = ImmutableArray.CreateBuilder<ContentNode>();
            var path = new HashSet<long>();
            foreach (var item in startingItems)
            {
                if (_emitted >= maximumItems)
                {
                    WasTruncated = true;
                    break;
                }

                var nodeResult = await ProjectNodeAsync(
                    item,
                    depth: 0,
                    includeChildren,
                    path,
                    cancellationToken);
                if (nodeResult is Result<ContentNode>.Failure failure)
                {
                    return failure.Error;
                }

                roots.Add(((Result<ContentNode>.Ok)nodeResult).Value);
            }

            return new HierarchyProjection(roots.ToImmutable(), WasTruncated);
        }

        private async Task<Result<ContentNode>> ProjectNodeAsync(
            ContentItem item,
            int depth,
            bool includeChildren,
            ISet<long> path,
            CancellationToken cancellationToken)
        {
            if (!path.Add(item.Id))
            {
                return AeroError.ValidationError(
                    ["The persisted content hierarchy contains a cycle."]);
            }

            _emitted++;
            var projectedChildren = ImmutableArray.CreateBuilder<ContentNode>();
            if (includeChildren)
            {
                var remaining = maximumItems - _emitted;
                var children = await owner.LoadChildrenAsync(
                    request,
                    contentTypeAlias,
                    culture,
                    item.Id,
                    Math.Max(1, remaining + 1),
                    cancellationToken);
                if (depth >= maximumDepth)
                {
                    WasTruncated = children.Count > 0 || WasTruncated;
                }
                else
                {
                    if (children.Count > remaining)
                    {
                        WasTruncated = true;
                    }

                    foreach (var child in children.Take(Math.Max(remaining, 0)))
                    {
                        if (_emitted >= maximumItems)
                        {
                            WasTruncated = true;
                            break;
                        }

                        var childResult = await ProjectNodeAsync(
                            child,
                            depth + 1,
                            includeChildren: true,
                            path,
                            cancellationToken);
                        if (childResult is Result<ContentNode>.Failure failure)
                        {
                            return failure.Error;
                        }

                        projectedChildren.Add(
                            ((Result<ContentNode>.Ok)childResult).Value);
                    }
                }
            }

            path.Remove(item.Id);
            return new ContentNode(
                item.Id.ToString(CultureInfo.InvariantCulture),
                item.ContentTypeAlias,
                item.Title ?? string.Empty,
                item.Slug,
                ProjectFields(item, projection),
                projectedChildren.ToImmutable());
        }
    }
}
