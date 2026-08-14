using Aero.Cms.Abstractions.Content.Views;
using Aero.Cms.Core.Content.Views;
using AeroDB.Sable;
using Shouldly;

namespace Aero.Cms.Core.Tests.Content;

public sealed class SableContentSurrealViewStoreTests
{
    [Test]
    public async Task Draft_and_published_state_filters_round_trip_without_chained_equality()
    {
        await using var harness = new SableTestHarness()
            .WithConfiguration(options => options.Schema.For<ContentSurrealViewDocument>()
                .TableName("content_surreal_view_revisions")
                .Identity(x => x.Id)
                .UniqueIndex(x => new { x.TenantId, x.SiteId, x.Alias, x.IsPublished, x.Version }));
        await harness.InitializeAsync();

        var scope = new ContentViewScope(101, 202);
        var store = new SableContentSurrealViewStore(harness.Session);
        var draft = await store.SaveDraftAsync(new ContentSurrealViewRevision(
            0,
            scope,
            "catalog",
            "catalog-entry",
            "shape-fingerprint",
            "SELECT external_id FROM catalog_read WHERE tenant_id = $tenantId AND site_id = $siteId LIMIT 20",
            "externalId",
            null,
            0,
            ContentViewPublicationState.Draft,
            DateTimeOffset.UtcNow,
            SourceAlias: "taxonomy_species_active",
            SourceSchemaFingerprint: "0123456789ABCDEF0123456789ABCDEF"));

        var loadedDraft = await store.LoadAsync(scope, "catalog", ContentViewPublicationState.Draft);
        var published = await store.PublishAsync(scope, "catalog", draft.Version);
        var loadedPublished = await store.LoadAsync(scope, "catalog", ContentViewPublicationState.Published);

        loadedDraft.ShouldNotBeNull();
        loadedDraft.Version.ShouldBe(draft.Version);
        loadedDraft.SourceAlias.ShouldBe("taxonomy_species_active");
        loadedDraft.SourceSchemaFingerprint.ShouldBe("0123456789ABCDEF0123456789ABCDEF");
        published.ShouldNotBeNull();
        loadedPublished.ShouldNotBeNull();
        loadedPublished.Version.ShouldBe(draft.Version);
        loadedPublished.PublicationState.ShouldBe(ContentViewPublicationState.Published);
        loadedPublished.SourceAlias.ShouldBe("taxonomy_species_active");
        loadedPublished.SourceSchemaFingerprint.ShouldBe("0123456789ABCDEF0123456789ABCDEF");
    }

    [Test]
    public async Task Separate_store_instances_allocate_distinct_immutable_draft_versions_through_the_database_unique_index()
    {
        await using var harness = new SableTestHarness()
            .WithConfiguration(options => options.Schema.For<ContentSurrealViewDocument>()
                .TableName("content_surreal_view_revisions")
                .Identity(x => x.Id)
                .UniqueIndex(x => new { x.TenantId, x.SiteId, x.Alias, x.IsPublished, x.Version }));
        await harness.InitializeAsync();
        await using var secondSession = await harness.OpenSessionAsync();

        var scope = new ContentViewScope(101, 202);
        var first = new SableContentSurrealViewStore(harness.Session);
        var second = new SableContentSurrealViewStore(secondSession);
        var draft = new ContentSurrealViewRevision(0, scope, "catalog", "catalog-entry", "shape-fingerprint",
            "SELECT external_id FROM catalog_read WHERE tenant_id = $tenantId AND site_id = $siteId LIMIT 20",
            "externalId", null, 0, ContentViewPublicationState.Draft, DateTimeOffset.UtcNow);

        var revisions = new[] { await first.SaveDraftAsync(draft), await second.SaveDraftAsync(draft) };

        var persisted = await harness.Session.Query<ContentSurrealViewDocument>().ToListAsync();
        persisted.Count.ShouldBe(2);
        revisions.Select(revision => revision.Version).Order().ShouldBe([1L, 2L]);
        (await first.LoadAsync(scope, "catalog", ContentViewPublicationState.Draft))!.Version.ShouldBe(2L);
    }
}
