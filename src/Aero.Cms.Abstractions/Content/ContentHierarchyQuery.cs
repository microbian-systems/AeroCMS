using System.Collections.Immutable;
using System.Text.Json;
using Aero.Core.Railway;

namespace Aero.Cms.Abstractions.Content;

/// <summary>Supported bounded traversals over hierarchical content items.</summary>
public enum ContentTraversal
{
    /// <summary>Returns root items without descendants.</summary>
    Roots = 0,

    /// <summary>Returns the direct children of <see cref="ContentQueryRequest.RootId"/>.</summary>
    Children = 1,

    /// <summary>Returns descendants below <see cref="ContentQueryRequest.RootId"/> as nested nodes.</summary>
    Descendants = 2,

    /// <summary>Returns ancestors of <see cref="ContentQueryRequest.RootId"/> in root-first order.</summary>
    Ancestors = 3,

    /// <summary>Returns every root with bounded nested descendants.</summary>
    RootsWithDescendants = 4
}

/// <summary>A trusted, prevalidated hierarchy query request.</summary>
/// <param name="Name">The page/template binding name.</param>
/// <param name="SiteId">The authoritative current site identifier.</param>
/// <param name="ContentTypeId">The authoritative stable content-type identifier.</param>
/// <param name="ContentTypeAlias">The allowed content type alias.</param>
/// <param name="Culture">The culture resolved before renderer execution.</param>
/// <param name="Traversal">The hierarchy traversal to execute.</param>
/// <param name="RootId">The optional traversal root identifier.</param>
/// <param name="MaximumDepth">The requested result depth bound.</param>
/// <param name="MaximumItems">The requested result item bound.</param>
/// <param name="Projection">The declared content fields to expose; empty exposes declared fields.</param>
/// <param name="IncludeDrafts">Whether the trusted preview boundary permits draft items.</param>
public sealed record ContentQueryRequest(
    string Name,
    long SiteId,
    long ContentTypeId,
    string ContentTypeAlias,
    string Culture,
    ContentTraversal Traversal,
    long? RootId = null,
    int MaximumDepth = 4,
    int MaximumItems = 100,
    ImmutableArray<string> Projection = default,
    bool IncludeDrafts = false);

/// <summary>
/// Executes trusted, eagerly materialized hierarchy requests without exposing
/// persistence or lazy query objects to a renderer.
/// </summary>
public interface IContentHierarchyQueryService
{
    /// <summary>Executes one authoritative site/culture-scoped hierarchy request.</summary>
    Task<Result<ContentQueryResult>> QueryAsync(
        ContentQueryRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>An immutable script-facing content node with eager children.</summary>
/// <param name="Id">The canonical decimal Snowflake identifier.</param>
/// <param name="ContentType">The content type alias.</param>
/// <param name="Title">The display title.</param>
/// <param name="Slug">The route-independent content slug.</param>
/// <param name="Fields">The projected immutable field values.</param>
/// <param name="Children">The eagerly materialized bounded children.</param>
public sealed record ContentNode(
    string Id,
    string ContentType,
    string Title,
    string Slug,
    ImmutableDictionary<string, JsonElement> Fields,
    ImmutableArray<ContentNode> Children);

/// <summary>An immutable, bounded hierarchy result safe to pass to renderers.</summary>
/// <param name="Name">The declared binding name.</param>
/// <param name="ContentTypeAlias">The authoritative current content-type alias.</param>
/// <param name="Roots">The eager result roots.</param>
/// <param name="TotalItems">The number of nodes present in this result.</param>
/// <param name="WasTruncated">Whether a candidate, depth, item, or output-size bound was reached.</param>
public sealed record ContentQueryResult(
    string Name,
    string ContentTypeAlias,
    ImmutableArray<ContentNode> Roots,
    int TotalItems,
    bool WasTruncated);
