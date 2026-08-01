using System.Security.Cryptography;
using System.Text;
using Aero.Cms.Abstractions.Ai.Knowledge;
using Aero.Cms.Abstractions.Ai.Pipeline;
using Aero.Cms.Core.Content.Search;
using Aero.Cms.Core.Entities;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;

namespace Aero.Cms.Modules.Ai.Knowledge;

/// <summary>
/// Replaces bounded public and manager knowledge projections inside the caller's unit of work.
/// </summary>
public sealed class AeroAiKnowledgeProjectionService(
    IDocumentSession session,
    IContentEmbeddingGenerator embeddingGenerator)
    : IAeroAiKnowledgeProjectionService
{
    public async Task StageUpsertAsync(
        AeroAiKnowledgeSource source,
        CancellationToken cancellationToken = default)
    {
        source = await ValidateAndScopeSourceAsync(source, cancellationToken);
        await DeletePriorChunksAsync(
            source.TenantId,
            source.SiteId,
            source.SourceKind,
            source.SourceId,
            cancellationToken);

        if (!source.IncludeInSearch)
            return;

        var managerSections = source.ManagerSections
            .Where(section => AeroAiContentExposureRules.IsFieldAvailable(
                AeroAiAudience.Manager,
                section.Exposure));
        await StageAudienceAsync(
            source,
            AeroAiAudience.Manager,
            managerSections,
            cancellationToken);

        if (!AeroAiContentExposureRules.IsEligibleForPublicAi(
                source.IsPublished,
                source.IncludeInSearch,
                source.IncludeInPublicAi))
        {
            return;
        }

        var publicSections = source.PublicSections
            .Where(section => AeroAiContentExposureRules.IsFieldAvailable(
                AeroAiAudience.Public,
                section.Exposure));
        await StageAudienceAsync(
            source,
            AeroAiAudience.Public,
            publicSections,
            cancellationToken);
    }

    public async Task StageDeleteAsync(
        long tenantId,
        long siteId,
        string sourceKind,
        long sourceId,
        CancellationToken cancellationToken = default)
    {
        if (siteId <= 0)
            throw new ArgumentOutOfRangeException(nameof(siteId));
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceKind);
        if (sourceId <= 0)
            throw new ArgumentOutOfRangeException(nameof(sourceId));

        var resolvedTenantId = await ResolveTenantIdAsync(
            tenantId,
            siteId,
            cancellationToken);
        await DeletePriorChunksAsync(
            resolvedTenantId,
            siteId,
            sourceKind,
            sourceId,
            cancellationToken);
    }

    private async Task StageAudienceAsync(
        AeroAiKnowledgeSource source,
        AeroAiAudience audience,
        IEnumerable<AeroAiKnowledgeSection> sections,
        CancellationToken cancellationToken)
    {
        var chunkRevision = 0;
        foreach (var section in sections)
        {
            if (string.IsNullOrWhiteSpace(section.Content))
                continue;
            if (section.Content.Length > AeroAiKnowledgeConstants.MaximumSectionCharacters)
            {
                throw new InvalidOperationException(
                    $"Knowledge section '{section.Name}' exceeds the bounded character limit.");
            }

            foreach (var content in AeroAiKnowledgeChunker.Chunk(section.Content))
            {
                if (chunkRevision >= AeroAiKnowledgeConstants.MaximumChunksPerSource)
                {
                    throw new InvalidOperationException(
                        $"Knowledge source '{source.SourceKind}:{source.SourceId}' exceeds the bounded chunk limit.");
                }

                var fullText = string.Join(
                    ' ',
                    new[] { source.Title, section.Name, content }
                        .Where(value => !string.IsNullOrWhiteSpace(value)));
                var document = new AeroAiKnowledgeChunkDocument
                {
                    Id = Snowflake.NewId(),
                    TenantId = source.TenantId,
                    SiteId = source.SiteId,
                    Audience = audience,
                    SourceKind = source.SourceKind,
                    SourceId = source.SourceId,
                    SourceUri = source.SourceUri,
                    Culture = source.Culture,
                    SourceRevision = source.SourceRevision,
                    ChunkRevision = chunkRevision++,
                    FieldExposure = section.Exposure,
                    IsPublished = source.IsPublished,
                    IncludeInSearch = source.IncludeInSearch,
                    IncludeInPublicAi = source.IncludeInPublicAi,
                    Title = source.Title,
                    Section = section.Name,
                    Content = content,
                    FullText = fullText,
                    ContentHash = Convert.ToHexString(
                        SHA256.HashData(Encoding.UTF8.GetBytes(fullText))),
                    GeneratedOn = DateTimeOffset.UtcNow
                };

                await TryAttachEmbeddingAsync(document, cancellationToken);
                session.Store(document);
            }
        }
    }

    private async Task TryAttachEmbeddingAsync(
        AeroAiKnowledgeChunkDocument document,
        CancellationToken cancellationToken)
    {
        if (!embeddingGenerator.IsAvailable)
            return;
        if (embeddingGenerator.Dimensions != AeroAiKnowledgeConstants.VectorDimensions)
        {
            throw new InvalidOperationException(
                $"Embedding generator '{embeddingGenerator.ModelId}' emits " +
                $"{embeddingGenerator.Dimensions} dimensions; " +
                $"{AeroAiKnowledgeConstants.VectorDimensions} are required.");
        }

        var generated = await embeddingGenerator.GenerateAsync(
            document.FullText,
            cancellationToken);
        if (generated is not Result<float[]>.Ok success)
            return;
        if (success.Value.Length != AeroAiKnowledgeConstants.VectorDimensions)
        {
            throw new InvalidOperationException(
                $"Embedding generator '{embeddingGenerator.ModelId}' returned " +
                $"{success.Value.Length} dimensions; " +
                $"{AeroAiKnowledgeConstants.VectorDimensions} are required.");
        }

        document.EmbeddingModelId = embeddingGenerator.ModelId;
        document.EmbeddingDimensions = embeddingGenerator.Dimensions;
        document.Embedding = success.Value;
    }

    private async Task DeletePriorChunksAsync(
        long tenantId,
        long siteId,
        string sourceKind,
        long sourceId,
        CancellationToken cancellationToken)
    {
        var prior = await session.Query<AeroAiKnowledgeChunkDocument>()
            .Where(chunk =>
                chunk.TenantId == tenantId
                && chunk.SiteId == siteId
                && chunk.SourceKind == sourceKind
                && chunk.SourceId == sourceId)
            .Take(AeroAiKnowledgeConstants.MaximumChunksPerSource * 2)
            .ToListAsync(cancellationToken);
        foreach (var chunk in prior)
            session.Delete(chunk);
    }

    private async Task<AeroAiKnowledgeSource> ValidateAndScopeSourceAsync(
        AeroAiKnowledgeSource source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.SiteId <= 0)
            throw new ArgumentOutOfRangeException(nameof(source.SiteId));
        ArgumentException.ThrowIfNullOrWhiteSpace(source.SourceKind);
        if (source.SourceId <= 0)
            throw new ArgumentOutOfRangeException(nameof(source.SourceId));
        ArgumentException.ThrowIfNullOrWhiteSpace(source.SourceUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(source.Culture);
        ArgumentNullException.ThrowIfNull(source.PublicSections);
        ArgumentNullException.ThrowIfNull(source.ManagerSections);
        var tenantId = await ResolveTenantIdAsync(
            source.TenantId,
            source.SiteId,
            cancellationToken);
        return source with { TenantId = tenantId };
    }

    private async Task<long> ResolveTenantIdAsync(
        long requestedTenantId,
        long siteId,
        CancellationToken cancellationToken)
    {
        var site = await session.LoadAsync<SitesModel>(siteId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Knowledge projection site '{siteId}' does not exist.");
        if (site.TenantId <= 0)
        {
            throw new InvalidOperationException(
                $"Knowledge projection site '{siteId}' has no valid tenant.");
        }
        if (requestedTenantId > 0 && requestedTenantId != site.TenantId)
        {
            throw new InvalidOperationException(
                "Knowledge projection tenant and site scopes do not match.");
        }

        return site.TenantId;
    }
}
