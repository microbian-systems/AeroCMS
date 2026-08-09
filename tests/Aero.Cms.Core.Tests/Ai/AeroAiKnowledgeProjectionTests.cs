using System.Security.Cryptography;
using System.Text;
using Aero.Cms.Abstractions.Ai.Knowledge;
using Aero.Cms.Abstractions.Ai.Pipeline;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Events;
using Aero.Cms.Core.Content.Search;
using Aero.Cms.Core.Entities;
using Aero.Cms.Html;
using Aero.Cms.Modules.Ai;
using Aero.Cms.Modules.Ai.Knowledge;
using Aero.Core.Railway;
using AeroDB.Sable;
using Shouldly;

namespace Aero.Cms.Core.Tests.Ai;

public sealed class AeroAiKnowledgeProjectionTests
{
    private const long TenantId = 41;
    private const long SiteId = 73;

    [Test]
    public async Task Projection_physically_separates_audiences_and_denies_sensitive_sections()
    {
        await using var harness = await CreateHarnessAsync();
        var service = CreateProjection(harness.Session);

        await service.StageUpsertAsync(CreateSource(
            publicSections:
            [
                Section("Public facts", "visible to everyone", AeroAiFieldExposure.Public),
                Section("Internal mistake", "must not cross the public plane", AeroAiFieldExposure.Internal),
                Section("Sensitive mistake", "must never be projected", AeroAiFieldExposure.Sensitive)
            ],
            managerSections:
            [
                Section("Public facts", "visible to everyone", AeroAiFieldExposure.Public),
                Section("Editorial notes", "manager context", AeroAiFieldExposure.Internal),
                Section("PII", "must never be projected", AeroAiFieldExposure.Sensitive),
                Section("Secret", "must never be projected", AeroAiFieldExposure.Secret)
            ]));
        await harness.Session.SaveChangesAsync();

        var chunks = await LoadChunksAsync(harness);
        var publicChunks = chunks
            .Where(chunk => chunk.Audience == AeroAiAudience.Public)
            .ToArray();
        var managerChunks = chunks
            .Where(chunk => chunk.Audience == AeroAiAudience.Manager)
            .ToArray();

        publicChunks.Select(chunk => chunk.Content)
            .ShouldBe(["visible to everyone"]);
        managerChunks.Select(chunk => chunk.Content)
            .ShouldBe(["visible to everyone", "manager context"]);
        chunks.ShouldNotContain(chunk =>
            chunk.Content.Contains("must never", StringComparison.Ordinal));
    }

    [Test]
    public async Task Unpublish_or_public_opt_out_removes_stale_public_chunks_but_keeps_manager_chunks()
    {
        await using var harness = await CreateHarnessAsync();
        var service = CreateProjection(harness.Session);
        var source = CreateSource(
            publicSections: [Section("Body", "published answer", AeroAiFieldExposure.Public)],
            managerSections: [Section("Draft", "editor answer", AeroAiFieldExposure.Internal)]);

        await service.StageUpsertAsync(source);
        await harness.Session.SaveChangesAsync();
        (await LoadChunksAsync(harness))
            .ShouldContain(chunk => chunk.Audience == AeroAiAudience.Public);

        await service.StageUpsertAsync(source with
        {
            IsPublished = false,
            SourceRevision = 2
        });
        await harness.Session.SaveChangesAsync();

        var chunks = await LoadChunksAsync(harness);
        chunks.ShouldNotContain(chunk => chunk.Audience == AeroAiAudience.Public);
        chunks.ShouldContain(chunk =>
            chunk.Audience == AeroAiAudience.Manager
            && chunk.Content == "editor answer");
    }

    [Test]
    public async Task Delete_invalidates_both_corpora()
    {
        await using var harness = await CreateHarnessAsync();
        var service = CreateProjection(harness.Session);
        await service.StageUpsertAsync(CreateSource(
            publicSections: [Section("Body", "public", AeroAiFieldExposure.Public)],
            managerSections: [Section("Body", "manager", AeroAiFieldExposure.Internal)]));
        await harness.Session.SaveChangesAsync();

        await service.StageDeleteAsync(
            TenantId,
            SiteId,
            AeroAiKnowledgeSourceKinds.Page,
            sourceId: 100);
        await harness.Session.SaveChangesAsync();

        (await LoadChunksAsync(harness)).ShouldBeEmpty();
    }

    [Test]
    public async Task Projection_rejects_a_tenant_and_site_mismatch()
    {
        await using var harness = await CreateHarnessAsync();
        var service = CreateProjection(harness.Session);
        var source = CreateSource(
            publicSections: [Section("Body", "public", AeroAiFieldExposure.Public)],
            managerSections: []);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => service.StageUpsertAsync(source with { TenantId = TenantId + 1 }));

        exception.Message.ShouldContain("tenant and site scopes do not match");
        (await LoadChunksAsync(harness)).ShouldBeEmpty();
    }

    [Test]
    public async Task Identical_projection_replay_preserves_ids_timestamps_hashes_and_embeddings()
    {
        await using var harness = await CreateHarnessAsync();
        var embeddings = new DeterministicEmbeddingGenerator();
        var service = CreateProjection(harness.Session, embeddings);
        var source = CreateSource(
            publicSections: [Section("Body", "public replay content", AeroAiFieldExposure.Public)],
            managerSections: [Section("Notes", "manager replay content", AeroAiFieldExposure.Internal)]);

        await service.StageUpsertAsync(source);
        await harness.Session.SaveChangesAsync();
        var first = (await LoadChunksAsync(harness))
            .OrderBy(chunk => chunk.Audience)
            .ThenBy(chunk => chunk.ChunkRevision)
            .ToArray();

        await service.StageUpsertAsync(source);
        await harness.Session.SaveChangesAsync();
        var replay = (await LoadChunksAsync(harness))
            .OrderBy(chunk => chunk.Audience)
            .ThenBy(chunk => chunk.ChunkRevision)
            .ToArray();

        replay.Length.ShouldBe(first.Length);
        embeddings.CallCount.ShouldBe(first.Length);
        for (var index = 0; index < first.Length; index++)
        {
            replay[index].Id.ShouldBe(first[index].Id);
            replay[index].GeneratedOn.ShouldBe(first[index].GeneratedOn);
            replay[index].ContentHash.ShouldBe(first[index].ContentHash);
            replay[index].EmbeddingModelId.ShouldBe(first[index].EmbeddingModelId);
            replay[index].EmbeddingDimensions.ShouldBe(first[index].EmbeddingDimensions);
            replay[index].Embedding.ShouldBe(first[index].Embedding);
        }
    }

    [Test]
    public async Task Changed_projection_reuses_matching_slot_and_removes_obsolete_slots()
    {
        await using var harness = await CreateHarnessAsync();
        var embeddings = new DeterministicEmbeddingGenerator();
        var service = CreateProjection(harness.Session, embeddings);
        var source = CreateSource(
            publicSections:
            [
                Section("Lead", "original lead", AeroAiFieldExposure.Public),
                Section("Tail", "obsolete tail", AeroAiFieldExposure.Public)
            ],
            managerSections: []);

        await service.StageUpsertAsync(source);
        await harness.Session.SaveChangesAsync();
        var original = (await LoadChunksAsync(harness))
            .OrderBy(chunk => chunk.ChunkRevision)
            .ToArray();
        original.Length.ShouldBe(2);
        var originalId = original[0].Id;
        var originalHash = original[0].ContentHash;
        var originalEmbedding = original[0].Embedding?.ToArray();

        await service.StageUpsertAsync(source with
        {
            SourceRevision = 2,
            PublicSections =
            [
                Section("Lead", "updated lead", AeroAiFieldExposure.Public)
            ]
        });
        await harness.Session.SaveChangesAsync();

        var changed = await LoadChunksAsync(harness);
        var retained = changed.ShouldHaveSingleItem();
        retained.Id.ShouldBe(originalId);
        retained.SourceRevision.ShouldBe(2);
        retained.ChunkRevision.ShouldBe(0);
        retained.Content.ShouldBe("updated lead");
        retained.ContentHash.ShouldNotBe(originalHash);
        retained.Embedding.ShouldNotBe(originalEmbedding);
        embeddings.CallCount.ShouldBe(3);
    }

    [Test]
    public async Task Projection_schema_rejects_duplicate_stable_slots()
    {
        await using var harness = await CreateHarnessAsync();
        var first = KnowledgeChunk(
            id: 801,
            tenantId: TenantId,
            siteId: SiteId,
            audience: AeroAiAudience.Public,
            content: "first slot");
        var duplicate = KnowledgeChunk(
            id: 802,
            tenantId: TenantId,
            siteId: SiteId,
            audience: AeroAiAudience.Public,
            content: "duplicate slot");
        duplicate.SourceId = first.SourceId;

        harness.Session.Store(first);
        harness.Session.Store(duplicate);

        await Should.ThrowAsync<InvalidOperationException>(
            () => harness.Session.SaveChangesAsync());
    }

    [Test]
    public async Task Page_event_projects_published_and_draft_snapshots_into_separate_corpora()
    {
        await using var harness = await CreateHarnessAsync(includePages: true);
        var page = new PageDocument
        {
            Id = 300,
            SiteId = SiteId,
            Culture = "en-US",
            Title = "Projection page",
            Slug = "projection",
            Path = "/projection",
            PublicationState = ContentPublicationState.Published,
            IncludeInSearch = true,
            IncludeInPublicAi = true,
            ContentRevision = 7,
            PublishedContent = Content(
                HtmlNode.CreateText("published body"),
                Element("script", HtmlNode.CreateText("dangerous source"))),
            DraftContent = Content(HtmlNode.CreateText("new draft body"))
        };
        harness.Session.Store(page);
        await harness.Session.SaveChangesAsync();

        var handler = new AeroAiKnowledgeProjectionHandler(
            harness.Session,
            CreateProjection(harness.Session));
        await handler.Handle(
            new PageContentUpdatedEvent(page.Id, SiteId, page.Slug),
            CancellationToken.None);

        var chunks = await LoadChunksAsync(harness);
        chunks.ShouldContain(chunk =>
            chunk.Audience == AeroAiAudience.Public
            && chunk.Content.Contains("published body", StringComparison.Ordinal));
        chunks.ShouldContain(chunk =>
            chunk.Audience == AeroAiAudience.Manager
            && chunk.Content.Contains("new draft body", StringComparison.Ordinal));
        chunks.ShouldNotContain(chunk =>
            chunk.Content.Contains("dangerous source", StringComparison.Ordinal));
        chunks.ShouldNotContain(chunk =>
            chunk.Audience == AeroAiAudience.Public
            && chunk.Content.Contains("new draft body", StringComparison.Ordinal));
    }

    [Test]
    public async Task Retrieval_applies_tenant_site_and_audience_scope_before_returning_candidates()
    {
        await using var harness = new SableTestHarness()
            .WithConfiguration(options => new AiModule().Configure(options));
        await harness.InitializeAsync();
        harness.Session.Store(KnowledgeChunk(
            id: 901,
            tenantId: TenantId,
            siteId: SiteId,
            audience: AeroAiAudience.Public,
            content: "orbital yak public answer"));
        harness.Session.Store(KnowledgeChunk(
            id: 902,
            tenantId: TenantId,
            siteId: SiteId,
            audience: AeroAiAudience.Manager,
            content: "orbital yak manager answer"));
        harness.Session.Store(KnowledgeChunk(
            id: 903,
            tenantId: TenantId + 1,
            siteId: SiteId,
            audience: AeroAiAudience.Public,
            content: "orbital yak other tenant"));
        harness.Session.Store(KnowledgeChunk(
            id: 904,
            tenantId: TenantId,
            siteId: SiteId + 1,
            audience: AeroAiAudience.Public,
            content: "orbital yak other site"));
        harness.Session.Store(new AeroManagerDocumentationChunkDocument
        {
            Id = 990,
            CorpusId = "aerocms-git-docs",
            TrustClass = "manager-internal",
            SourceAudience = "manager-internal",
            SourceId = 989,
            SourceUri = "/reference/orbital-yaks",
            Culture = "en-US",
            SourceRevision = 1,
            ChunkRevision = 0,
            Title = "Orbital yak operations",
            FeatureArea = "Operations",
            Maturity = "stable",
            Section = "Manager guidance",
            Content = "orbital yak manager documentation",
            FullText = "Orbital yak operations Manager guidance orbital yak manager documentation",
            ContentHash = "documentation-hash",
            GeneratedOn = DateTimeOffset.UtcNow
        });
        await harness.Session.SaveChangesAsync();

        var retriever = new AeroAiKnowledgeRetriever(
            harness.Session,
            new UnavailableContentEmbeddingGenerator());
        var publicResult = await retriever.SearchAsync(new AeroAiKnowledgeQuery(
            TenantId,
            SiteId,
            AeroAiAudience.Public,
            "en-US",
            "orbital yak"));
        var memberResult = await retriever.SearchAsync(new AeroAiKnowledgeQuery(
            TenantId,
            SiteId,
            AeroAiAudience.Member,
            "en-US",
            "orbital yak"));
        var managerResult = await retriever.SearchAsync(new AeroAiKnowledgeQuery(
            TenantId,
            SiteId,
            AeroAiAudience.Manager,
            "en-US",
            "orbital yak"));

        publicResult.ShouldBeOfType<Result<IReadOnlyList<AeroAiKnowledgeMatch>>.Ok>()
            .Value.Select(match => match.ChunkId)
            .ShouldBe([901]);
        memberResult.ShouldBeOfType<Result<IReadOnlyList<AeroAiKnowledgeMatch>>.Ok>()
            .Value.Select(match => match.ChunkId)
            .ShouldBe([901]);
        managerResult.ShouldBeOfType<Result<IReadOnlyList<AeroAiKnowledgeMatch>>.Ok>()
            .Value.Select(match => match.ChunkId)
            .ShouldBe([990, 902]);
    }

    private static AeroAiKnowledgeProjectionService CreateProjection(
        IDocumentSession session)
        => new(session, new UnavailableContentEmbeddingGenerator());

    private static AeroAiKnowledgeProjectionService CreateProjection(
        IDocumentSession session,
        IContentEmbeddingGenerator embeddingGenerator)
        => new(session, embeddingGenerator);

    private static AeroAiKnowledgeSource CreateSource(
        IReadOnlyList<AeroAiKnowledgeSection> publicSections,
        IReadOnlyList<AeroAiKnowledgeSection> managerSections)
        => new(
            TenantId,
            SiteId,
            AeroAiKnowledgeSourceKinds.Page,
            SourceId: 100,
            SourceUri: "/projection",
            Culture: "en-US",
            SourceRevision: 1,
            IsPublished: true,
            IncludeInSearch: true,
            IncludeInPublicAi: true,
            Title: "Projection",
            PublicSections: publicSections,
            ManagerSections: managerSections);

    private static AeroAiKnowledgeSection Section(
        string name,
        string content,
        AeroAiFieldExposure exposure)
        => new(name, content, exposure);

    private static HtmlPageContent Content(params HtmlNode[] children)
    {
        var content = new HtmlPageContent();
        content.Root.Children.AddRange(children);
        return content;
    }

    private static HtmlNode Element(string tagName, params HtmlNode[] children)
    {
        var element = HtmlNode.CreateElement(tagName);
        element.Children.AddRange(children);
        return element;
    }

    private static async Task<SableTestHarness> CreateHarnessAsync(
        bool includePages = false)
    {
        var harness = new SableTestHarness()
            .WithSchema<SitesModel>(SchemaMode.Flexible)
            .WithConfiguration(options => new AiModule().Configure(options));
        if (includePages)
            harness.WithSchema<PageDocument>(SchemaMode.Flexible);

        await harness.InitializeAsync();
        harness.Session.Store(new SitesModel
        {
            Id = SiteId,
            TenantId = TenantId,
            Name = "Knowledge test",
            IsEnabled = true
        });
        await harness.Session.SaveChangesAsync();
        return harness;
    }

    private static async Task<List<AeroAiKnowledgeChunkDocument>> LoadChunksAsync(
        SableTestHarness harness)
    {
        await using var session = await harness.OpenSessionAsync();
        return await session.Query<AeroAiKnowledgeChunkDocument>()
            .Where(chunk =>
                chunk.TenantId == TenantId
                && chunk.SiteId == SiteId)
            .ToListAsync();
    }

    private static AeroAiKnowledgeChunkDocument KnowledgeChunk(
        long id,
        long tenantId,
        long siteId,
        AeroAiAudience audience,
        string content)
        => new()
        {
            Id = id,
            TenantId = tenantId,
            SiteId = siteId,
            Audience = audience,
            SourceKind = AeroAiKnowledgeSourceKinds.Page,
            SourceId = id,
            SourceUri = $"/knowledge/{id}",
            Culture = "en-US",
            SourceRevision = 1,
            ChunkRevision = 0,
            FieldExposure = AeroAiFieldExposure.Public,
            IsPublished = true,
            IncludeInSearch = true,
            IncludeInPublicAi = true,
            Title = "Orbital yak",
            Section = "Body",
            Content = content,
            FullText = $"Orbital yak Body {content}",
            ContentHash = $"hash-{id}",
            GeneratedOn = DateTimeOffset.UtcNow
        };

    private sealed class DeterministicEmbeddingGenerator : IContentEmbeddingGenerator
    {
        public string ModelId => "projection-test-embedding";
        public int Dimensions => AeroAiKnowledgeConstants.VectorDimensions;
        public bool IsAvailable => true;
        public int CallCount { get; private set; }

        public Task<Result<float[]>> GenerateAsync(
            string text,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            var digest = SHA256.HashData(Encoding.UTF8.GetBytes(text));
            return Task.FromResult<Result<float[]>>(
                Enumerable.Range(0, Dimensions)
                    .Select(index => digest[index % digest.Length] / 255F)
                    .ToArray());
        }
    }
}
