using Aero.Cms.Abstractions.Content;
using Aero.Cms.Core.Content.Search;
using Aero.Core.Railway;
using AeroDB.Sable;

namespace Aero.Cms.Core.Content.Indexing;

/// <summary>Stages search projection changes in the caller's Sable unit of work.</summary>
public sealed class ContentSearchProjectionService(
    IDocumentSession session,
    ContentIndexService indexService,
    IContentEmbeddingGenerator embeddingGenerator)
{
    public async Task StageUpsertAsync(
        ContentItem item,
        ContentTypeDefinition definition,
        CancellationToken cancellationToken = default)
    {
        var artifacts = indexService.BuildIndex(item, definition);
        if (artifacts.Facets.Count > ContentSearchConstants.MaximumFacetsPerItem)
        {
            throw new InvalidOperationException(
                $"Content item '{item.Id}' produced {artifacts.Facets.Count} exact-search facets; " +
                $"the bounded limit is {ContentSearchConstants.MaximumFacetsPerItem}.");
        }

        await DeletePriorFacetsAsync(item.Id, cancellationToken);
        session.Delete<ContentSearchDocument>(item.Id);
        session.Delete<ContentSemanticDocument>(item.Id);

        session.Store(artifacts.Document);
        foreach (var facet in artifacts.Facets)
        {
            session.Store(facet);
        }

        if (definition.HideFromSearch
            || string.IsNullOrWhiteSpace(artifacts.SemanticText)
            || !embeddingGenerator.IsAvailable)
        {
            return;
        }

        if (embeddingGenerator.Dimensions != ContentSearchConstants.VectorDimensions)
        {
            throw new InvalidOperationException(
                $"Embedding generator '{embeddingGenerator.ModelId}' emits {embeddingGenerator.Dimensions} dimensions; " +
                $"the content search schema requires {ContentSearchConstants.VectorDimensions}.");
        }

        var generated = await embeddingGenerator.GenerateAsync(
            artifacts.SemanticText,
            cancellationToken);
        if (generated is not Result<float[]>.Ok success)
        {
            return;
        }

        if (success.Value.Length != embeddingGenerator.Dimensions)
        {
            throw new InvalidOperationException(
                $"Embedding generator '{embeddingGenerator.ModelId}' returned {success.Value.Length} dimensions; " +
                $"{embeddingGenerator.Dimensions} were expected.");
        }

        session.Store(new ContentSemanticDocument
        {
            Id = item.Id,
            SiteId = item.SiteId,
            ContentItemId = item.Id,
            ContentTypeAlias = item.ContentTypeAlias,
            Culture = item.Culture,
            PublicationState = item.PublicationState,
            PublishedOn = item.PublishedOn,
            HideFromSearch = definition.HideFromSearch,
            ModelId = embeddingGenerator.ModelId,
            EmbeddingDimensions = embeddingGenerator.Dimensions,
            Embedding = success.Value
        });
    }

    public async Task StageDeleteAsync(
        long contentItemId,
        CancellationToken cancellationToken = default)
    {
        await DeletePriorFacetsAsync(contentItemId, cancellationToken);
        session.Delete<ContentSearchDocument>(contentItemId);
        session.Delete<ContentSemanticDocument>(contentItemId);
    }

    private async Task DeletePriorFacetsAsync(
        long contentItemId,
        CancellationToken cancellationToken)
    {
        var priorFacets = await session.Query<ContentSearchFacet>()
            .Where(facet => facet.ContentItemId == contentItemId)
            .Take(ContentSearchConstants.MaximumFacetsPerItem)
            .ToListAsync(cancellationToken);
        foreach (var facet in priorFacets)
        {
            session.Delete(facet);
        }
    }
}
