using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Serialization;
using Aero.Cms.Abstractions.Events;
using Aero.Cms.Core.Entities;
using AeroDB.Sable;

namespace Aero.Cms.Modules.Pages;

/// <summary>
/// Projects page-composition events into first-class tree documents and flattened
/// node indexes. This is the new page-builder read side; LayoutRegions are kept
/// only as a compatibility bridge for legacy block rendering.
/// </summary>
public sealed class PageCompositionProjection : IProjection
{
        /// <summary>
    /// Apply method.
    /// </summary>
public void Apply(IDocumentOperations operations, IReadOnlyList<IEvent> events)
    {
        foreach (var @event in CompositionEvents(events))
        {
            ApplyEvent(operations, @event);
        }
    }

        /// <summary>
    /// ApplyAsync method.
    /// </summary>
public Task ApplyAsync(IDocumentOperations operations, IReadOnlyList<IEvent> events, CancellationToken ct)
    {
        Apply(operations, events);
        return Task.CompletedTask;
    }

    private static IEnumerable<IEvent> CompositionEvents(IEnumerable<IEvent> events)
        => events.Where(e => e.Data is PageCompositionDraftSaved or PageCompositionPublished);

    private static void ApplyEvent(IDocumentOperations operations, IEvent @event)
    {
        switch (@event.Data)
        {
            case PageCompositionDraftSaved e:
                StoreComposition(
                    operations,
                    e.CompositionId,
                    e.PageId,
                    e.SiteId,
                    PageCompositionState.Draft,
                    e.Culture,
                    e.ContentRevision,
                    publishedVersion: 0,
                    e.Title,
                    e.Slug,
                    e.Summary,
                    e.SeoTitle,
                    e.SeoDescription,
                    e.Kind,
                    e.ShowHeaderNavigation,
                    e.HeaderImageUrl,
                    e.HideHeader,
                    e.HideFooter,
                    e.ShowChatAgent,
                    e.RootNodes,
                    e.LayoutRegions,
                    e.BlockIdMap);
                break;

            case PageCompositionPublished e:
                StoreComposition(
                    operations,
                    e.PublishedCompositionId,
                    e.PageId,
                    e.SiteId,
                    PageCompositionState.Published,
                    e.Culture,
                    contentRevision: 0,
                    e.PublishedVersion,
                    e.Title,
                    e.Slug,
                    e.Summary,
                    e.SeoTitle,
                    e.SeoDescription,
                    e.Kind,
                    e.ShowHeaderNavigation,
                    e.HeaderImageUrl,
                    e.HideHeader,
                    e.HideFooter,
                    e.ShowChatAgent,
                    e.RootNodes,
                    e.LayoutRegions,
                    e.BlockIdMap);
                break;
        }
    }

    private static void StoreComposition(
        IDocumentOperations operations,
        long compositionId,
        long pageId,
        long siteId,
        PageCompositionState state,
        string culture,
        long contentRevision,
        long publishedVersion,
        string title,
        string slug,
        string? summary,
        string? seoTitle,
        string? seoDescription,
        Aero.Cms.Abstractions.Enums.PageKind kind,
        bool showHeaderNavigation,
        string? headerImageUrl,
        bool hideHeader,
        bool hideFooter,
        bool showChatAgent,
        IReadOnlyList<NeoPageNode> rootNodes,
        IReadOnlyList<Aero.Cms.Abstractions.Blocks.Layout.LayoutRegion>? layoutRegions,
        IReadOnlyDictionary<string, long>? blockIdMap)
    {
        var composition = new PageCompositionDocument
        {
            Id = compositionId,
            SiteId = siteId,
            PageId = pageId,
            Culture = culture,
            State = state,
            ContentRevision = contentRevision,
            PublishedVersion = publishedVersion,
            Title = title,
            Slug = slug,
            Summary = summary,
            SeoTitle = seoTitle,
            SeoDescription = seoDescription,
            Kind = kind,
            ShowHeaderNavigation = showHeaderNavigation,
            HeaderImageUrl = headerImageUrl,
            HideHeader = hideHeader,
            HideFooter = hideFooter,
            ShowChatAgent = showChatAgent,
            RootNodes = rootNodes.Select(CloneNode).ToList(),
            LayoutRegions = layoutRegions?.ToList() ?? [],
            BlockIdMap = blockIdMap is null ? [] : new Dictionary<string, long>(blockIdMap)
        };

        operations.Store(composition);

        foreach (var indexedNode in FlattenNodes(siteId, pageId, compositionId, culture, composition.RootNodes))
        {
            operations.Store(indexedNode);
        }
    }

    private static NeoPageNode CloneNode(NeoPageNode node)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(node, BlockJsonContext.Default.NeoPageNode);
        return System.Text.Json.JsonSerializer.Deserialize(json, BlockJsonContext.Default.NeoPageNode)!;
    }

    private static IEnumerable<PageNodeIndexDocument> FlattenNodes(
        long siteId,
        long pageId,
        long compositionId,
        string culture,
        IReadOnlyList<NeoPageNode> nodes)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            foreach (var node in FlattenNode(siteId, pageId, compositionId, culture, nodes[i], $"root/{i}", 0))
            {
                yield return node;
            }
        }
    }

    private static IEnumerable<PageNodeIndexDocument> FlattenNode(
        long siteId,
        long pageId,
        long compositionId,
        string culture,
        NeoPageNode node,
        string path,
        int depth)
    {
        yield return new PageNodeIndexDocument
        {
            Id = $"{compositionId}:{node.NodeId}",
            SiteId = siteId,
            PageId = pageId,
            CompositionId = compositionId,
            Culture = culture,
            NodeId = node.NodeId,
            CatalogId = node.CatalogId,
            Kind = node.Kind,
            Path = path,
            Depth = depth
        };

        for (var i = 0; i < node.Children.Count; i++)
        {
            foreach (var child in FlattenNode(siteId, pageId, compositionId, culture, node.Children[i], $"{path}/{i}", depth + 1))
            {
                yield return child;
            }
        }
    }
}
