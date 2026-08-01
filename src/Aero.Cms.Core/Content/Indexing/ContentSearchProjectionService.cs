using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Ai.Knowledge;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Core.Content.Search;
using Aero.Core.Railway;
using AeroDB.Sable;

namespace Aero.Cms.Core.Content.Indexing;

/// <summary>Stages search projection changes in the caller's Sable unit of work.</summary>
public sealed class ContentSearchProjectionService(
    IDocumentSession session,
    ContentIndexService indexService,
    IContentEmbeddingGenerator embeddingGenerator,
    IAeroAiKnowledgeProjectionService? knowledgeProjectionService = null)
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
        await StageKnowledgeAsync(item, definition, artifacts, cancellationToken);

        if (!definition.IncludeInSearch
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
            HideFromSearch = !definition.IncludeInSearch,
            ModelId = embeddingGenerator.ModelId,
            EmbeddingDimensions = embeddingGenerator.Dimensions,
            Embedding = success.Value
        });
    }

    public async Task StageDeleteAsync(
        long siteId,
        long contentItemId,
        CancellationToken cancellationToken = default)
    {
        await DeletePriorFacetsAsync(contentItemId, cancellationToken);
        session.Delete<ContentSearchDocument>(contentItemId);
        session.Delete<ContentSemanticDocument>(contentItemId);
        if (knowledgeProjectionService is not null)
        {
            await knowledgeProjectionService.StageDeleteAsync(
                tenantId: 0,
                siteId,
                AeroAiKnowledgeSourceKinds.ContentItem,
                contentItemId,
                cancellationToken);
        }
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

    private Task StageKnowledgeAsync(
        ContentItem item,
        ContentTypeDefinition definition,
        ContentSearchArtifacts artifacts,
        CancellationToken cancellationToken)
        => knowledgeProjectionService is null
            ? Task.CompletedTask
            : knowledgeProjectionService.StageUpsertAsync(
                new AeroAiKnowledgeSource(
                    TenantId: 0,
                    item.SiteId,
                    AeroAiKnowledgeSourceKinds.ContentItem,
                    item.Id,
                    $"/api/v1/query/content/{Uri.EscapeDataString(item.ContentTypeAlias)}" +
                    $"?rootId={item.Id}",
                    item.Culture,
                    item.VersionNumber,
                    item.PublicationState == ContentPublicationState.Published,
                    definition.IncludeInSearch,
                    definition.IncludeInPublicAi,
                    item.Title ?? item.Slug,
                    artifacts.PublicKnowledgeSections,
                    artifacts.ManagerKnowledgeSections),
                cancellationToken);
}
