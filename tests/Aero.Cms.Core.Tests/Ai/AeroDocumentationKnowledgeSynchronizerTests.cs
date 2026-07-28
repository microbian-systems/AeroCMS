using Aero.Cms.Abstractions.Ai.Knowledge;
using Aero.Cms.Core.Content.Search;
using Aero.Cms.Modules.Ai;
using Aero.Cms.Modules.Ai.Knowledge;
using Aero.Core.Railway;
using AeroDB.Sable;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Aero.Cms.Core.Tests.Ai;

public sealed class AeroDocumentationKnowledgeSynchronizerTests
{
    [Test]
    public async Task Synchronization_persists_a_stable_vectorized_projection_and_removes_stale_chunks()
    {
        await using var harness = new SableTestHarness()
            .WithConfiguration(options => new AiModule().Configure(options));
        await harness.InitializeAsync();
        var source = new StubDocumentationKnowledgeSource(
            Snapshot(Chunk(101, "first-hash", "Commerce subscriptions")));
        var embeddings = new StubEmbeddingGenerator();
        var synchronizer = new AeroDocumentationKnowledgeSynchronizer(
            harness.Session,
            source,
            embeddings,
            NullLogger<AeroDocumentationKnowledgeSynchronizer>.Instance);

        var first = await synchronizer.SynchronizeAsync();
        var firstSuccess = first
            .ShouldBeOfType<Result<AeroDocumentationSyncResult>.Ok>()
            .Value;
        firstSuccess.Created.ShouldBe(1);
        firstSuccess.Updated.ShouldBe(0);
        firstSuccess.Deleted.ShouldBe(0);
        firstSuccess.Embedded.ShouldBe(1);

        await using (var read = await harness.OpenSessionAsync())
        {
            var chunk = await read.LoadAsync<AeroManagerDocumentationChunkDocument>(101);
            chunk.ShouldNotBeNull();
            chunk!.SourceUri.ShouldBe("/guides/commerce");
            chunk.Embedding.ShouldNotBeNull();
            chunk.Embedding!.Length.ShouldBe(ContentSearchConstants.VectorDimensions);

            var state = await read.LoadAsync<AeroManagerDocumentationCorpusStateDocument>(1);
            state.ShouldNotBeNull();
            state!.CorpusHash.ShouldBe("corpus-hash-1");
            state.SearchSchemaVersion.ShouldBe(AeroAiKnowledgeConstants.SchemaVersion);
            state.ChunkCount.ShouldBe(1);
            state.EmbeddedChunkCount.ShouldBe(1);
            state.EmbeddingsReady.ShouldBeTrue();
            state.EmbeddingModelId.ShouldBe(embeddings.ModelId);
        }

        var second = await synchronizer.SynchronizeAsync();
        var secondSuccess = second
            .ShouldBeOfType<Result<AeroDocumentationSyncResult>.Ok>()
            .Value;
        secondSuccess.Created.ShouldBe(0);
        secondSuccess.Updated.ShouldBe(0);
        secondSuccess.Deleted.ShouldBe(0);
        embeddings.CallCount.ShouldBe(1);

        source.Snapshot = Snapshot(
            Chunk(101, "second-hash", "Updated Commerce subscriptions"),
            corpusHash: "corpus-hash-2");
        var updated = await synchronizer.SynchronizeAsync();
        var updatedSuccess = updated
            .ShouldBeOfType<Result<AeroDocumentationSyncResult>.Ok>()
            .Value;
        updatedSuccess.Updated.ShouldBe(1);
        updatedSuccess.Embedded.ShouldBe(1);
        embeddings.CallCount.ShouldBe(2);

        source.Snapshot = Snapshot(corpusHash: "corpus-hash-3");
        var removed = await synchronizer.SynchronizeAsync();
        removed.ShouldBeOfType<Result<AeroDocumentationSyncResult>.Ok>()
            .Value.Deleted.ShouldBe(1);

        await using var finalRead = await harness.OpenSessionAsync();
        (await finalRead.Query<AeroManagerDocumentationChunkDocument>()
                .ToListAsync())
            .ShouldBeEmpty();
    }

    [Test]
    public async Task Synchronization_keeps_full_text_available_without_an_embedding_provider()
    {
        await using var harness = new SableTestHarness()
            .WithConfiguration(options => new AiModule().Configure(options));
        await harness.InitializeAsync();
        var synchronizer = new AeroDocumentationKnowledgeSynchronizer(
            harness.Session,
            new StubDocumentationKnowledgeSource(
                Snapshot(Chunk(201, "text-hash", "Full text only"))),
            new UnavailableContentEmbeddingGenerator(),
            NullLogger<AeroDocumentationKnowledgeSynchronizer>.Instance);

        var result = await synchronizer.SynchronizeAsync();

        result.ShouldBeOfType<Result<AeroDocumentationSyncResult>.Ok>()
            .Value.Embedded.ShouldBe(0);
        await using var read = await harness.OpenSessionAsync();
        var chunk = await read.LoadAsync<AeroManagerDocumentationChunkDocument>(201);
        chunk.ShouldNotBeNull();
        chunk!.FullText.ShouldContain("Full text only");
        chunk.Embedding.ShouldBeNull();
        var state = await read.LoadAsync<AeroManagerDocumentationCorpusStateDocument>(1);
        state.ShouldNotBeNull();
        state!.EmbeddingsReady.ShouldBeFalse();
    }

    [Test]
    public async Task Synchronization_retries_missing_vectors_on_the_next_pass()
    {
        await using var harness = new SableTestHarness()
            .WithConfiguration(options => new AiModule().Configure(options));
        await harness.InitializeAsync();
        var embeddings = new FailOnceEmbeddingGenerator();
        var synchronizer = new AeroDocumentationKnowledgeSynchronizer(
            harness.Session,
            new StubDocumentationKnowledgeSource(
                Snapshot(Chunk(301, "retry-hash", "Retry vectorization"))),
            embeddings,
            NullLogger<AeroDocumentationKnowledgeSynchronizer>.Instance);

        var first = await synchronizer.SynchronizeAsync();
        var firstSuccess = first
            .ShouldBeOfType<Result<AeroDocumentationSyncResult>.Ok>()
            .Value;
        firstSuccess.EmbeddingsReady.ShouldBeFalse();
        firstSuccess.Embedded.ShouldBe(0);

        var second = await synchronizer.SynchronizeAsync();
        var secondSuccess = second
            .ShouldBeOfType<Result<AeroDocumentationSyncResult>.Ok>()
            .Value;
        secondSuccess.EmbeddingsReady.ShouldBeTrue();
        secondSuccess.Embedded.ShouldBe(1);
        embeddings.CallCount.ShouldBe(2);
    }

    private static AeroDocumentationKnowledgeSnapshot Snapshot(
        AeroDocumentationKnowledgeChunk chunk,
        string corpusHash = "corpus-hash-1")
        => Snapshot([chunk], corpusHash);

    private static AeroDocumentationKnowledgeSnapshot Snapshot(
        string corpusHash = "corpus-hash-1")
        => Snapshot([], corpusHash);

    private static AeroDocumentationKnowledgeSnapshot Snapshot(
        IReadOnlyList<AeroDocumentationKnowledgeChunk> chunks,
        string corpusHash)
        => new(
            SchemaVersion: 1,
            Product: "AeroCMS",
            LastVerifiedCommit: "test-commit",
            SourceRevision: 81,
            TrustClass: "manager",
            CorpusHash: corpusHash,
            Chunks: chunks);

    private static AeroDocumentationKnowledgeChunk Chunk(
        long id,
        string contentHash,
        string content)
        => new(
            Id: id,
            SourceId: 77,
            CanonicalPath: "/guides/commerce",
            Culture: "en-US",
            Title: "Commerce",
            FeatureArea: "Commerce",
            Maturity: "stable",
            SourceAudience: "public",
            Section: "Subscriptions",
            Content: content,
            FullText: $"Commerce Subscriptions {content}",
            SourceRevision: 81,
            ChunkRevision: 0,
            ContentHash: contentHash,
            TrustClass: "public-documentation");

    private sealed class StubDocumentationKnowledgeSource(
        AeroDocumentationKnowledgeSnapshot snapshot)
        : IAeroDocumentationKnowledgeSource
    {
        public AeroDocumentationKnowledgeSnapshot Snapshot { get; set; } = snapshot;

        public AeroDocumentationKnowledgeSnapshot GetSnapshot() => Snapshot;

        public IReadOnlyList<AeroAiKnowledgeMatch> Search(string query, int take) => [];
    }

    private sealed class StubEmbeddingGenerator : IContentEmbeddingGenerator
    {
        public string ModelId => "test-embedding";
        public int Dimensions => ContentSearchConstants.VectorDimensions;
        public bool IsAvailable => true;
        public int CallCount { get; private set; }

        public Task<Result<float[]>> GenerateAsync(
            string text,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult<Result<float[]>>(
                Enumerable.Repeat(0.25f, Dimensions).ToArray());
        }
    }

    private sealed class FailOnceEmbeddingGenerator : IContentEmbeddingGenerator
    {
        public string ModelId => "retry-embedding";
        public int Dimensions => ContentSearchConstants.VectorDimensions;
        public bool IsAvailable => true;
        public int CallCount { get; private set; }

        public Task<Result<float[]>> GenerateAsync(
            string text,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            if (CallCount == 1)
            {
                return Task.FromResult<Result<float[]>>(
                    Aero.Core.AeroError.ValidationError(
                        ["Transient embedding failure."]));
            }

            return Task.FromResult<Result<float[]>>(
                Enumerable.Repeat(0.5f, Dimensions).ToArray());
        }
    }
}
