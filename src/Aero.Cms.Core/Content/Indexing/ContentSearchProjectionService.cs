using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Content.Localization;
using Aero.Cms.Abstractions.Ai.Knowledge;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Core.Content.Search;
using Aero.Core.Railway;
using AeroDB.Sable;
using System.Text.Json;

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
        IReadOnlyDictionary<string, JsonElement> authoritativeSharedFields,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(authoritativeSharedFields);

        // ContentTranslationGroupDocument is the single durable owner of shared
        // values. Build all projections from a detached logical item so stale or
        // accidentally persisted values on a variant cannot leak into search.
        var projectionItem = CreateProjectionSnapshot(item, definition, authoritativeSharedFields);
        var artifacts = indexService.BuildIndex(projectionItem, definition);
        if (artifacts.Facets.Count > ContentSearchConstants.MaximumFacetsPerItem)
        {
            throw new InvalidOperationException(
                $"Content item '{item.Id}' produced {artifacts.Facets.Count} exact-search facets; " +
                $"the bounded limit is {ContentSearchConstants.MaximumFacetsPerItem}.");
        }

        await DeletePriorFacetsAsync(projectionItem.Id, cancellationToken);
        session.Delete<ContentSearchDocument>(projectionItem.Id);
        session.Delete<ContentSemanticDocument>(projectionItem.Id);

        session.Store(artifacts.Document);
        foreach (var facet in artifacts.Facets)
        {
            session.Store(facet);
        }
        await StageKnowledgeAsync(projectionItem, definition, artifacts, cancellationToken);

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
            Id = projectionItem.Id,
            SiteId = projectionItem.SiteId,
            ContentItemId = projectionItem.Id,
            ContentTypeAlias = projectionItem.ContentTypeAlias,
            Culture = projectionItem.Culture,
            PublicationState = projectionItem.PublicationState,
            PublishedOn = projectionItem.PublishedOn,
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

    private static ContentItem CreateProjectionSnapshot(
        ContentItem item,
        ContentTypeDefinition definition,
        IReadOnlyDictionary<string, JsonElement> authoritativeSharedFields)
    {
        var sharedNames = definition.Fields
            .Where(field => field.LocalizationMode == ContentFieldLocalizationMode.Shared)
            .Select(field => field.Name)
            .ToHashSet(StringComparer.Ordinal);
        var fields = item.Fields
            .Where(pair => !sharedNames.Contains(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value.Clone(), item.Fields.Comparer);
        foreach (var (name, value) in authoritativeSharedFields)
        {
            if (sharedNames.Contains(name))
            {
                fields[name] = value.Clone();
            }
        }

        return new ContentItem
        {
            Id = item.Id,
            Version = item.Version,
            SiteId = item.SiteId,
            ContentTypeAlias = item.ContentTypeAlias,
            Slug = item.Slug,
            Title = item.Title,
            TranslationGroupId = item.TranslationGroupId,
            Culture = item.Culture,
            SourceItemId = item.SourceItemId,
            ParentId = item.ParentId,
            SortOrder = item.SortOrder,
            Fields = fields,
            PublicationState = item.PublicationState,
            PublishedOn = item.PublishedOn,
            VersionNumber = item.VersionNumber,
            SchedulePublishUtc = item.SchedulePublishUtc,
            ScheduleUnpublishUtc = item.ScheduleUnpublishUtc,
            CreatedOn = item.CreatedOn,
            ModifiedOn = item.ModifiedOn,
            CreatedBy = item.CreatedBy,
            ModifiedBy = item.ModifiedBy,
            TranslationProvenance = item.TranslationProvenance,
            TranslationReview = item.TranslationReview
        };
    }
}
