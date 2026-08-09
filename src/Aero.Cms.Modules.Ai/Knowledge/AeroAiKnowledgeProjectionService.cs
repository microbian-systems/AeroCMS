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
        var existing = await LoadPriorChunksAsync(
            source.TenantId,
            source.SiteId,
            source.SourceKind,
            source.SourceId,
            cancellationToken);

        var desired = new List<AeroAiKnowledgeChunkDocument>();
        if (source.IncludeInSearch)
        {
            var managerSections = source.ManagerSections
                .Where(section => AeroAiContentExposureRules.IsFieldAvailable(
                    AeroAiAudience.Manager,
                    section.Exposure));
            StageAudience(
                desired,
                source,
                AeroAiAudience.Manager,
                managerSections);

            if (AeroAiContentExposureRules.IsEligibleForPublicAi(
                    source.IsPublished,
                    source.IncludeInSearch,
                    source.IncludeInPublicAi))
            {
                var publicSections = source.PublicSections
                    .Where(section => AeroAiContentExposureRules.IsFieldAvailable(
                        AeroAiAudience.Public,
                        section.Exposure));
                StageAudience(
                    desired,
                    source,
                    AeroAiAudience.Public,
                    publicSections);
            }
        }

        await ReconcileAsync(existing, desired, cancellationToken);
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
        var prior = await LoadPriorChunksAsync(
            resolvedTenantId,
            siteId,
            sourceKind,
            sourceId,
            cancellationToken);
        foreach (var chunk in prior)
            session.Delete(chunk);
    }

    private static void StageAudience(
        ICollection<AeroAiKnowledgeChunkDocument> desired,
        AeroAiKnowledgeSource source,
        AeroAiAudience audience,
        IEnumerable<AeroAiKnowledgeSection> sections)
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
                        SHA256.HashData(Encoding.UTF8.GetBytes(fullText)))
                };

                desired.Add(document);
            }
        }
    }

    private async Task TryAttachEmbeddingAsync(
        AeroAiKnowledgeChunkDocument document,
        AeroAiKnowledgeChunkDocument? prior,
        CancellationToken cancellationToken)
    {
        if (!embeddingGenerator.IsAvailable)
        {
            if (prior?.ContentHash == document.ContentHash)
                PreserveEmbedding(document, prior);
            return;
        }
        if (embeddingGenerator.Dimensions != AeroAiKnowledgeConstants.VectorDimensions)
        {
            throw new InvalidOperationException(
                $"Embedding generator '{embeddingGenerator.ModelId}' emits " +
                $"{embeddingGenerator.Dimensions} dimensions; " +
                $"{AeroAiKnowledgeConstants.VectorDimensions} are required.");
        }

        if (prior is not null
            && prior.ContentHash == document.ContentHash
            && string.Equals(
                prior.EmbeddingModelId,
                embeddingGenerator.ModelId,
                StringComparison.Ordinal)
            && prior.EmbeddingDimensions == embeddingGenerator.Dimensions
            && prior.Embedding?.Length == embeddingGenerator.Dimensions)
        {
            PreserveEmbedding(document, prior);
            return;
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

    private async Task ReconcileAsync(
        IReadOnlyCollection<AeroAiKnowledgeChunkDocument> existing,
        IReadOnlyCollection<AeroAiKnowledgeChunkDocument> desired,
        CancellationToken cancellationToken)
    {
        var existingBySlot = existing.ToDictionary(KnowledgeChunkSlot.From);
        foreach (var candidate in desired)
        {
            var slot = KnowledgeChunkSlot.From(candidate);
            existingBySlot.Remove(slot, out var prior);
            candidate.Id = prior?.Id ?? Snowflake.NewId();
            candidate.GeneratedOn = prior?.GeneratedOn ?? DateTimeOffset.UtcNow;
            await TryAttachEmbeddingAsync(candidate, prior, cancellationToken);

            if (prior is null)
            {
                session.Store(candidate);
                continue;
            }

            if (Equivalent(prior, candidate))
                continue;

            Copy(candidate, prior);
            session.Update(prior);
        }

        foreach (var obsolete in existingBySlot.Values)
            session.Delete(obsolete);
    }

    private async Task<List<AeroAiKnowledgeChunkDocument>> LoadPriorChunksAsync(
        long tenantId,
        long siteId,
        string sourceKind,
        long sourceId,
        CancellationToken cancellationToken)
    {
        return await session.Query<AeroAiKnowledgeChunkDocument>()
            .Where(chunk =>
                chunk.TenantId == tenantId
                && chunk.SiteId == siteId
                && chunk.SourceKind == sourceKind
                && chunk.SourceId == sourceId)
            .Take(AeroAiKnowledgeConstants.MaximumChunksPerSource * 2)
            .ToListAsync(cancellationToken);
    }

    private static void PreserveEmbedding(
        AeroAiKnowledgeChunkDocument target,
        AeroAiKnowledgeChunkDocument source)
    {
        target.EmbeddingModelId = source.EmbeddingModelId;
        target.EmbeddingDimensions = source.EmbeddingDimensions;
        target.Embedding = source.Embedding;
    }

    private static bool Equivalent(
        AeroAiKnowledgeChunkDocument left,
        AeroAiKnowledgeChunkDocument right)
        => left.TenantId == right.TenantId
           && left.SiteId == right.SiteId
           && left.Audience == right.Audience
           && left.SourceKind == right.SourceKind
           && left.SourceId == right.SourceId
           && left.SourceUri == right.SourceUri
           && left.Culture == right.Culture
           && left.SourceRevision == right.SourceRevision
           && left.ChunkRevision == right.ChunkRevision
           && left.SearchSchemaVersion == right.SearchSchemaVersion
           && left.FieldExposure == right.FieldExposure
           && left.IsPublished == right.IsPublished
           && left.IncludeInSearch == right.IncludeInSearch
           && left.IncludeInPublicAi == right.IncludeInPublicAi
           && left.Title == right.Title
           && left.Section == right.Section
           && left.Content == right.Content
           && left.FullText == right.FullText
           && left.ContentHash == right.ContentHash
           && left.EmbeddingModelId == right.EmbeddingModelId
           && left.EmbeddingDimensions == right.EmbeddingDimensions
           && EqualVectors(left.Embedding, right.Embedding);

    private static bool EqualVectors(float[]? left, float[]? right)
        => ReferenceEquals(left, right)
           || left is not null
           && right is not null
           && left.AsSpan().SequenceEqual(right);

    private static void Copy(
        AeroAiKnowledgeChunkDocument source,
        AeroAiKnowledgeChunkDocument target)
    {
        target.TenantId = source.TenantId;
        target.SiteId = source.SiteId;
        target.Audience = source.Audience;
        target.SourceKind = source.SourceKind;
        target.SourceId = source.SourceId;
        target.SourceUri = source.SourceUri;
        target.Culture = source.Culture;
        target.SourceRevision = source.SourceRevision;
        target.ChunkRevision = source.ChunkRevision;
        target.SearchSchemaVersion = source.SearchSchemaVersion;
        target.FieldExposure = source.FieldExposure;
        target.IsPublished = source.IsPublished;
        target.IncludeInSearch = source.IncludeInSearch;
        target.IncludeInPublicAi = source.IncludeInPublicAi;
        target.Title = source.Title;
        target.Section = source.Section;
        target.Content = source.Content;
        target.FullText = source.FullText;
        target.ContentHash = source.ContentHash;
        target.GeneratedOn = DateTimeOffset.UtcNow;
        target.EmbeddingModelId = source.EmbeddingModelId;
        target.EmbeddingDimensions = source.EmbeddingDimensions;
        target.Embedding = source.Embedding;
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

    private readonly record struct KnowledgeChunkSlot(
        long TenantId,
        long SiteId,
        string SourceKind,
        long SourceId,
        AeroAiAudience Audience,
        int ChunkRevision,
        int SearchSchemaVersion)
    {
        public static KnowledgeChunkSlot From(AeroAiKnowledgeChunkDocument document)
            => new(
                document.TenantId,
                document.SiteId,
                document.SourceKind,
                document.SourceId,
                document.Audience,
                document.ChunkRevision,
                document.SearchSchemaVersion);
    }
}
