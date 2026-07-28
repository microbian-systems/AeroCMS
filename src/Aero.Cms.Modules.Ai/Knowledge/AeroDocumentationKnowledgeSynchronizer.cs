using Aero.Cms.Core.Content.Search;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Ai.Knowledge;

public sealed record AeroDocumentationSyncResult(
    int Created,
    int Updated,
    int Deleted,
    int Embedded,
    int ChunkCount,
    bool EmbeddingsReady,
    bool EmbeddingProviderAvailable,
    long SourceRevision);

public interface IAeroDocumentationKnowledgeSynchronizer
{
    Task<Result<AeroDocumentationSyncResult>> SynchronizeAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Reconciles the build-embedded documentation snapshot into its disposable Sable projection.
/// </summary>
public sealed class AeroDocumentationKnowledgeSynchronizer(
    IDocumentSession session,
    IAeroDocumentationKnowledgeSource source,
    IContentEmbeddingGenerator embeddingGenerator,
    ILogger<AeroDocumentationKnowledgeSynchronizer> logger)
    : IAeroDocumentationKnowledgeSynchronizer
{
    private const int MaximumDocumentationChunks = 4_096;

    public async Task<Result<AeroDocumentationSyncResult>> SynchronizeAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            session.Concurrency = ConcurrencyChecks.Enabled;
            var snapshot = source.GetSnapshot();
            if (snapshot.Chunks.Count > MaximumDocumentationChunks)
            {
                return AeroError.ValidationError(
                    [$"The AeroCMS documentation corpus exceeds the {MaximumDocumentationChunks}-chunk limit."]);
            }

            if (embeddingGenerator.IsAvailable
                && embeddingGenerator.Dimensions != AeroAiKnowledgeConstants.VectorDimensions)
            {
                return AeroError.ValidationError(
                    [$"The configured embedding generator must emit {AeroAiKnowledgeConstants.VectorDimensions} dimensions."]);
            }

            var state = await session.LoadAsync<AeroManagerDocumentationCorpusStateDocument>(
                AeroDocumentationKnowledgeConstants.CorpusStateId,
                cancellationToken);
            if (IsCurrent(state, snapshot))
            {
                return new AeroDocumentationSyncResult(
                    0,
                    0,
                    0,
                    state!.EmbeddedChunkCount,
                    state.ChunkCount,
                    state.EmbeddingsReady,
                    embeddingGenerator.IsAvailable,
                    snapshot.SourceRevision);
            }

            var existing = await session.Query<AeroManagerDocumentationChunkDocument>()
                .Where(document =>
                    document.CorpusId == AeroDocumentationKnowledgeConstants.CorpusId)
                .Take(MaximumDocumentationChunks + 1)
                .ToListAsync(cancellationToken);
            if (existing.Count > MaximumDocumentationChunks)
            {
                return AeroError.ValidationError(
                    ["The persisted AeroCMS documentation projection exceeds its reconciliation limit."]);
            }

            var desiredIds = new HashSet<long>();
            var existingById = existing.ToDictionary(document => document.Id);
            var created = 0;
            var updated = 0;
            var embedded = 0;

            foreach (var chunk in snapshot.Chunks)
            {
                if (!desiredIds.Add(chunk.Id))
                {
                    return AeroError.ValidationError(
                        [$"The AeroCMS documentation corpus contains duplicate chunk id '{chunk.Id}'."]);
                }

                existingById.TryGetValue(chunk.Id, out var prior);
                var candidate = CreateDocument(chunk, snapshot, prior);
                await AttachEmbeddingAsync(candidate, prior, cancellationToken);
                if (candidate.Embedding is { Length: > 0 })
                    embedded++;

                if (prior is null)
                {
                    session.Store(candidate);
                    created++;
                    continue;
                }

                if (Equivalent(prior, candidate))
                    continue;

                Copy(candidate, prior);
                session.Update(prior);
                updated++;
            }

            var stale = existing
                .Where(document => !desiredIds.Contains(document.Id))
                .ToArray();
            foreach (var document in stale)
                session.Delete(document);

            var embeddingsReady = embeddingGenerator.IsAvailable
                && embedded == snapshot.Chunks.Count;
            var nextState = new AeroManagerDocumentationCorpusStateDocument
            {
                Id = AeroDocumentationKnowledgeConstants.CorpusStateId,
                CorpusId = AeroDocumentationKnowledgeConstants.CorpusId,
                SchemaVersion = snapshot.SchemaVersion,
                SearchSchemaVersion = AeroAiKnowledgeConstants.SchemaVersion,
                GitCommit = snapshot.LastVerifiedCommit,
                CorpusHash = snapshot.CorpusHash,
                ChunkCount = snapshot.Chunks.Count,
                EmbeddedChunkCount = embedded,
                EmbeddingModelId = embeddingsReady ? embeddingGenerator.ModelId : null,
                EmbeddingDimensions = embeddingsReady ? embeddingGenerator.Dimensions : null,
                EmbeddingsReady = embeddingsReady,
                ReconciledOn = DateTimeOffset.UtcNow
            };
            if (state is null)
            {
                session.Store(nextState);
            }
            else
            {
                Copy(nextState, state);
                session.Update(state);
            }

            await session.SaveChangesAsync(cancellationToken);

            return new AeroDocumentationSyncResult(
                created,
                updated,
                stale.Length,
                embedded,
                snapshot.Chunks.Count,
                embeddingsReady,
                embeddingGenerator.IsAvailable,
                snapshot.SourceRevision);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Unable to reconcile the AeroCMS documentation knowledge projection.");
            return AeroError.DatabaseError(
                "The AeroCMS documentation knowledge projection could not be synchronized.");
        }
    }

    private bool IsCurrent(
        AeroManagerDocumentationCorpusStateDocument? state,
        AeroDocumentationKnowledgeSnapshot snapshot)
        => state is not null
           && state.CorpusId == AeroDocumentationKnowledgeConstants.CorpusId
           && state.SchemaVersion == snapshot.SchemaVersion
           && state.SearchSchemaVersion == AeroAiKnowledgeConstants.SchemaVersion
           && state.GitCommit == snapshot.LastVerifiedCommit
           && state.CorpusHash == snapshot.CorpusHash
           && state.ChunkCount == snapshot.Chunks.Count
           && (!embeddingGenerator.IsAvailable
               || state.EmbeddingsReady
               && state.EmbeddingModelId == embeddingGenerator.ModelId
               && state.EmbeddingDimensions == embeddingGenerator.Dimensions
               && state.EmbeddedChunkCount == snapshot.Chunks.Count);

    private static AeroManagerDocumentationChunkDocument CreateDocument(
        AeroDocumentationKnowledgeChunk chunk,
        AeroDocumentationKnowledgeSnapshot snapshot,
        AeroManagerDocumentationChunkDocument? prior)
        => new()
        {
            Id = chunk.Id,
            CorpusId = AeroDocumentationKnowledgeConstants.CorpusId,
            TrustClass = snapshot.TrustClass,
            SourceAudience = chunk.SourceAudience,
            SourceId = chunk.SourceId,
            SourceUri = chunk.CanonicalPath,
            Culture = chunk.Culture,
            SourceRevision = snapshot.SourceRevision,
            ChunkRevision = chunk.ChunkRevision,
            SearchSchemaVersion = AeroAiKnowledgeConstants.SchemaVersion,
            Title = chunk.Title,
            FeatureArea = chunk.FeatureArea,
            Maturity = chunk.Maturity,
            Section = chunk.Section,
            Content = chunk.Content,
            FullText = chunk.FullText,
            ContentHash = chunk.ContentHash,
            GeneratedOn = prior?.GeneratedOn ?? DateTimeOffset.UtcNow
        };

    private async Task AttachEmbeddingAsync(
        AeroManagerDocumentationChunkDocument candidate,
        AeroManagerDocumentationChunkDocument? prior,
        CancellationToken cancellationToken)
    {
        if (!embeddingGenerator.IsAvailable)
        {
            if (prior?.ContentHash == candidate.ContentHash)
                PreserveEmbedding(candidate, prior);
            return;
        }

        if (prior is not null
            && prior.ContentHash == candidate.ContentHash
            && string.Equals(
                prior.EmbeddingModelId,
                embeddingGenerator.ModelId,
                StringComparison.Ordinal)
            && prior.EmbeddingDimensions == embeddingGenerator.Dimensions
            && prior.Embedding?.Length == embeddingGenerator.Dimensions)
        {
            PreserveEmbedding(candidate, prior);
            return;
        }

        var generated = await embeddingGenerator.GenerateAsync(
            candidate.FullText,
            cancellationToken);
        if (generated is not Result<float[]>.Ok success)
        {
            logger.LogWarning(
                "Embedding generation failed for AeroCMS documentation chunk {ChunkId}; full-text indexing will remain available.",
                candidate.Id);
            return;
        }

        if (success.Value.Length != AeroAiKnowledgeConstants.VectorDimensions)
        {
            throw new InvalidOperationException(
                $"Embedding generator '{embeddingGenerator.ModelId}' returned " +
                $"{success.Value.Length} dimensions; " +
                $"{AeroAiKnowledgeConstants.VectorDimensions} are required.");
        }

        candidate.EmbeddingModelId = embeddingGenerator.ModelId;
        candidate.EmbeddingDimensions = embeddingGenerator.Dimensions;
        candidate.Embedding = success.Value;
    }

    private static void PreserveEmbedding(
        AeroManagerDocumentationChunkDocument target,
        AeroManagerDocumentationChunkDocument? source)
    {
        target.EmbeddingModelId = source?.EmbeddingModelId;
        target.EmbeddingDimensions = source?.EmbeddingDimensions;
        target.Embedding = source?.Embedding;
    }

    private static bool Equivalent(
        AeroManagerDocumentationChunkDocument left,
        AeroManagerDocumentationChunkDocument right)
        => left.CorpusId == right.CorpusId
           && left.TrustClass == right.TrustClass
           && left.SourceAudience == right.SourceAudience
           && left.SourceId == right.SourceId
           && left.SourceUri == right.SourceUri
           && left.Culture == right.Culture
           && left.SourceRevision == right.SourceRevision
           && left.ChunkRevision == right.ChunkRevision
           && left.SearchSchemaVersion == right.SearchSchemaVersion
           && left.Title == right.Title
           && left.FeatureArea == right.FeatureArea
           && left.Maturity == right.Maturity
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
        AeroManagerDocumentationChunkDocument source,
        AeroManagerDocumentationChunkDocument target)
    {
        target.CorpusId = source.CorpusId;
        target.TrustClass = source.TrustClass;
        target.SourceAudience = source.SourceAudience;
        target.SourceId = source.SourceId;
        target.SourceUri = source.SourceUri;
        target.Culture = source.Culture;
        target.SourceRevision = source.SourceRevision;
        target.ChunkRevision = source.ChunkRevision;
        target.SearchSchemaVersion = source.SearchSchemaVersion;
        target.Title = source.Title;
        target.FeatureArea = source.FeatureArea;
        target.Maturity = source.Maturity;
        target.Section = source.Section;
        target.Content = source.Content;
        target.FullText = source.FullText;
        target.ContentHash = source.ContentHash;
        target.GeneratedOn = DateTimeOffset.UtcNow;
        target.EmbeddingModelId = source.EmbeddingModelId;
        target.EmbeddingDimensions = source.EmbeddingDimensions;
        target.Embedding = source.Embedding;
    }

    private static void Copy(
        AeroManagerDocumentationCorpusStateDocument source,
        AeroManagerDocumentationCorpusStateDocument target)
    {
        target.CorpusId = source.CorpusId;
        target.SchemaVersion = source.SchemaVersion;
        target.SearchSchemaVersion = source.SearchSchemaVersion;
        target.GitCommit = source.GitCommit;
        target.CorpusHash = source.CorpusHash;
        target.ChunkCount = source.ChunkCount;
        target.EmbeddedChunkCount = source.EmbeddedChunkCount;
        target.EmbeddingModelId = source.EmbeddingModelId;
        target.EmbeddingDimensions = source.EmbeddingDimensions;
        target.EmbeddingsReady = source.EmbeddingsReady;
        target.ReconciledOn = source.ReconciledOn;
    }
}

/// <summary>
/// Starts one asynchronous reconciliation pass when the AI module host starts.
/// </summary>
public sealed class AeroDocumentationKnowledgeSyncHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<AeroDocumentationKnowledgeSyncHostedService> logger)
    : BackgroundService
{
    private const int MaximumAttempts = 3;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        for (var attempt = 1; attempt <= MaximumAttempts; attempt++)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            if (scope.ServiceProvider.GetService<IDocumentStore>() is null)
            {
                logger.LogDebug(
                    "Skipping AeroCMS documentation ingestion because no Sable document store is registered.");
                return;
            }

            var synchronizer = scope.ServiceProvider
                .GetRequiredService<IAeroDocumentationKnowledgeSynchronizer>();
            var result = await synchronizer.SynchronizeAsync(stoppingToken);
            if (result is Result<AeroDocumentationSyncResult>.Failure failure)
            {
                if (attempt == MaximumAttempts)
                {
                    logger.LogError(
                        "AeroCMS documentation ingestion failed after {Attempts} attempts: {Error}",
                        MaximumAttempts,
                        failure.Error);
                    return;
                }

                logger.LogWarning(
                    "AeroCMS documentation ingestion attempt {Attempt} failed and will be retried: {Error}",
                    attempt,
                    failure.Error);
                await DelayBeforeRetryAsync(attempt, stoppingToken);
                continue;
            }

            var success = ((Result<AeroDocumentationSyncResult>.Ok)result).Value;
            logger.LogInformation(
                "AeroCMS documentation ingestion complete. Created {Created}, updated {Updated}, deleted {Deleted}, vectorized {Embedded}/{ChunkCount}; revision {Revision}.",
                success.Created,
                success.Updated,
                success.Deleted,
                success.Embedded,
                success.ChunkCount,
                success.SourceRevision);

            if (!success.EmbeddingProviderAvailable || success.EmbeddingsReady)
                return;

            if (attempt == MaximumAttempts)
            {
                logger.LogWarning(
                    "AeroCMS documentation full-text ingestion completed, but vectorization remained incomplete after {Attempts} attempts.",
                    MaximumAttempts);
                return;
            }

            logger.LogWarning(
                "AeroCMS documentation vectorization is incomplete ({Embedded}/{ChunkCount}); retrying attempt {NextAttempt}.",
                success.Embedded,
                success.ChunkCount,
                attempt + 1);
            await DelayBeforeRetryAsync(attempt, stoppingToken);
        }
    }

    private static Task DelayBeforeRetryAsync(
        int completedAttempt,
        CancellationToken cancellationToken)
        => Task.Delay(TimeSpan.FromSeconds(completedAttempt), cancellationToken);
}
