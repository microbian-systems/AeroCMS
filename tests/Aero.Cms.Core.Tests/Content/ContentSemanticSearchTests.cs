using Aero.Cms.Abstractions.Content;
using Aero.Cms.Core.Content.Search;
using Aero.Cms.Core.Content.Services;
using Aero.Core.Railway;
using AeroDB.Sable;
using NSubstitute;

namespace Aero.Cms.Core.Tests.Content;

public sealed class ContentSemanticSearchTests
{
    [Test]
    public async Task Semantic_search_fails_closed_without_an_embedding_provider()
    {
        var service = new AeroContentQueryService(
            Substitute.For<IDocumentSession>());

        var result = await service.SearchIndexAsync(new ContentSearchRequest(
            1,
            "animal",
            "social canid",
            "en-US",
            ContentSearchMode.Semantic,
            PublishedOnly: true,
            Skip: 0,
            Take: 10,
            new Dictionary<string, string>()));

        var failure = result as Result<ContentSearchResult>.Failure;
        await Assert.That(failure).IsNotNull();
        var validation = failure!.Error as Aero.Core.AeroError.Validation;
        await Assert.That(validation).IsNotNull();
        await Assert.That(validation!.Errors.Any(error =>
            error.Contains("embedding generator", StringComparison.OrdinalIgnoreCase)))
            .IsTrue();
    }

    [Test]
    public async Task Semantic_search_does_not_mix_vectors_from_another_model()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<ContentItem>(SchemaMode.Flexible)
            .WithSchema<ContentSemanticDocument>(SchemaMode.Flexible)
            .WithConfiguration(options =>
            {
                options.Schema.For<ContentSemanticDocument>()
                    .HnswIndex(
                        document => document.Embedding,
                        ContentSearchConstants.VectorDimensions,
                        Search.Distance.Cosine);
            });
        await harness.InitializeAsync();
        var vector = UnitVector();
        harness.Session.Store(
            new ContentItem
            {
                Id = 1,
                SiteId = 1,
                ContentTypeAlias = "animal",
                Culture = "en-US",
                Title = "Wolf",
                Slug = "wolf"
            },
            new ContentItem
            {
                Id = 2,
                SiteId = 1,
                ContentTypeAlias = "animal",
                Culture = "en-US",
                Title = "Dog",
                Slug = "dog"
            });
        harness.Session.Store(
            SemanticDocument(1, "model-a", vector),
            SemanticDocument(2, "model-b", vector));
        await harness.Session.SaveChangesAsync();
        var service = new AeroContentQueryService(
            harness.Session,
            new DeterministicEmbeddingGenerator("model-a", vector));

        var result = await service.SearchIndexAsync(new ContentSearchRequest(
            1,
            "animal",
            "social canid",
            "en-US",
            ContentSearchMode.Semantic,
            PublishedOnly: false,
            Skip: 0,
            Take: 10,
            new Dictionary<string, string>()));

        var success = result as Result<ContentSearchResult>.Ok;
        await Assert.That(success).IsNotNull();
        await Assert.That(success!.Value.Items.Select(item => item.Id))
            .IsEquivalentTo([1L]);
    }

    [Test]
    public async Task Semantic_search_scopes_before_bounded_candidate_selection()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<ContentItem>(SchemaMode.Flexible)
            .WithSchema<ContentSemanticDocument>(SchemaMode.Flexible)
            .WithConfiguration(options =>
            {
                options.Schema.For<ContentSemanticDocument>()
                    .HnswIndex(
                        document => document.Embedding,
                        ContentSearchConstants.VectorDimensions,
                        Search.Distance.Cosine);
            });
        await harness.InitializeAsync();

        var queryVector = UnitVector();
        var validVector = UnitVector();
        validVector[0] = 0.95f;
        validVector[1] = 0.3122499f;
        harness.Session.Store(new ContentItem
        {
            Id = 1,
            SiteId = 1,
            ContentTypeAlias = "animal",
            Culture = "en-US",
            Title = "Wolf",
            Slug = "wolf"
        });
        harness.Session.Store(SemanticDocument(1, "model-a", validVector));

        for (var index = 0;
             index <= ContentSearchConstants.MaximumCandidates;
             index++)
        {
            var foreign = SemanticDocument(
                10_000 + index,
                "model-a",
                queryVector);
            foreign.SiteId = 2;
            harness.Session.Store(foreign);
        }

        await harness.Session.SaveChangesAsync();
        var service = new AeroContentQueryService(
            harness.Session,
            new DeterministicEmbeddingGenerator("model-a", queryVector));

        var result = await service.SearchIndexAsync(new ContentSearchRequest(
            1,
            "animal",
            "social canid",
            "en-US",
            ContentSearchMode.Semantic,
            PublishedOnly: false,
            Skip: 0,
            Take: 10,
            new Dictionary<string, string>()));

        var success = result as Result<ContentSearchResult>.Ok;
        await Assert.That(success).IsNotNull();
        await Assert.That(success!.Value.Items.Select(item => item.Id))
            .IsEquivalentTo([1L]);
    }

    [Test]
    public async Task Semantic_search_rejects_a_provider_that_returns_the_wrong_vector_size()
    {
        var session = Substitute.For<IDocumentSession>();
        var service = new AeroContentQueryService(
            session,
            new MisreportingEmbeddingGenerator());

        var result = await service.SearchIndexAsync(new ContentSearchRequest(
            1,
            "animal",
            "social canid",
            "en-US",
            ContentSearchMode.Semantic,
            PublishedOnly: false,
            Skip: 0,
            Take: 10,
            new Dictionary<string, string>()));

        var failure = result as Result<ContentSearchResult>.Failure;
        await Assert.That(failure).IsNotNull();
        var validation = failure!.Error as Aero.Core.AeroError.Validation;
        await Assert.That(validation).IsNotNull();
        await Assert.That(validation!.Errors.Any(error =>
            error.Contains("returned 1 dimensions", StringComparison.OrdinalIgnoreCase)))
            .IsTrue();
    }

    private static ContentSemanticDocument SemanticDocument(
        long id,
        string modelId,
        float[] vector) =>
        new()
        {
            Id = id,
            SiteId = 1,
            ContentItemId = id,
            ContentTypeAlias = "animal",
            Culture = "en-US",
            ModelId = modelId,
            EmbeddingDimensions = ContentSearchConstants.VectorDimensions,
            Embedding = vector
        };

    private static float[] UnitVector()
    {
        var vector = new float[ContentSearchConstants.VectorDimensions];
        vector[0] = 1;
        return vector;
    }

    private sealed class DeterministicEmbeddingGenerator(
        string modelId,
        float[] vector) : IContentEmbeddingGenerator
    {
        public string ModelId => modelId;
        public int Dimensions => vector.Length;
        public bool IsAvailable => true;

        public Task<Result<float[]>> GenerateAsync(
            string text,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Result<float[]>>(
                new Result<float[]>.Ok(vector));
    }

    private sealed class MisreportingEmbeddingGenerator
        : IContentEmbeddingGenerator
    {
        public string ModelId => "model-a";
        public int Dimensions => ContentSearchConstants.VectorDimensions;
        public bool IsAvailable => true;

        public Task<Result<float[]>> GenerateAsync(
            string text,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Result<float[]>>(
                new Result<float[]>.Ok([1f]));
    }
}
