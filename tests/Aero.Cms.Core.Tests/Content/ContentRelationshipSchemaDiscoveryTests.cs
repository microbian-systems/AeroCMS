using Aero.Cms.Abstractions.Content.Views;
using Aero.Cms.Core.Content.Views;
using Shouldly;

namespace Aero.Cms.Core.Tests.Content;

public sealed class ContentRelationshipSchemaDiscoveryTests
{
    [Test]
    public async Task Sable_metadata_reader_deserializes_the_info_for_database_object_map()
    {
        await using var harness = new SableTestHarness();
        await harness.InitializeAsync();
        await harness.Session.ExecuteSqlAsync("DEFINE TABLE registered_catalog_read SCHEMAFULL;");
        var reader = new SableContentSchemaMetadataReader(harness.Session);

        var definitions = await reader.ReadTableDefinitionsAsync();

        definitions.ShouldContainKey("registered_catalog_read");
        definitions["registered_catalog_read"].ShouldContain("DEFINE TABLE registered_catalog_read");
    }

    [Test]
    public async Task Registered_record_links_and_edges_are_discovered_as_external_read_only_metadata()
    {
        var reader = new StubReader(new Dictionary<string, string>
        {
            ["product"] = "DEFINE FIELD category ON TABLE product TYPE record<category>;",
            ["related"] = """
                DEFINE TABLE related TYPE RELATION IN product OUT category SCHEMAFULL;
                DEFINE FIELD tenant_id ON TABLE related TYPE int ASSERT $value != NONE;
                DEFINE FIELD site_id ON TABLE related TYPE int ASSERT $value != NONE;
                """
        });
        var targets = new ContentPhysicalSchemaTargetRegistry([
            new("product-shape", "product"), new("category-shape", "category"),
            new("edge-shape", "related")]);
        var discovery = new SableContentRelationshipSchemaDiscovery(reader, targets);

        var relationships = await discovery.DiscoverAsync(new ContentViewScope(3, 5));

        relationships.Count.ShouldBe(2);
        relationships.ShouldAllBe(item => item.OwnershipState == ContentRelationshipOwnershipState.ExternalDiscovered && item.IsReadOnly);
        relationships.Single(item => item.Kind == ContentRelationshipKind.RecordLink).SourceShapeAlias.ShouldBe("product-shape");
        var edge = relationships.Single(item => item.Kind == ContentRelationshipKind.GraphEdge);
        edge.TargetShapeAlias.ShouldBe("category-shape");
        edge.SchemaFingerprint.ShouldBe(ContentRelationshipDdlLifecycle.CreateFingerprint(
            ContentRelationshipDdlLifecycle.CreateStatements(edge)));
    }

    [Test]
    public async Task Array_record_links_preserve_one_to_many_cardinality()
    {
        var reader = new StubReader(new Dictionary<string, string>
        {
            ["product"] = "DEFINE FIELD categories ON TABLE product TYPE array<record<category>>;"
        });
        var discovery = new SableContentRelationshipSchemaDiscovery(reader,
            new ContentPhysicalSchemaTargetRegistry([new("product", "product"), new("category", "category")]));

        var link = (await discovery.DiscoverAsync(new ContentViewScope(3, 5))).Single();

        link.Cardinality.ShouldBe(ContentRelationshipCardinality.OneToMany);
        link.SourceField.ShouldBe("categories");
    }

    [Test]
    public async Task Graph_edges_without_complete_scope_invariants_are_not_trusted_as_existing_relationships()
    {
        var reader = new StubReader(new Dictionary<string, string>
        {
            ["catalog_product"] = """
                DEFINE TABLE catalog_product TYPE RELATION IN catalog OUT product SCHEMAFULL;
                DEFINE FIELD tenant_id ON TABLE catalog_product TYPE int ASSERT $value != NONE;
                """
        });
        var discovery = new SableContentRelationshipSchemaDiscovery(reader,
            new ContentPhysicalSchemaTargetRegistry([
                new("catalog-shape", "catalog"), new("product-shape", "product"), new("edge-shape", "catalog_product")]));

        (await discovery.DiscoverAsync(new ContentViewScope(3, 5))).ShouldBeEmpty();
    }

    [Test]
    public async Task Graph_edges_with_weakened_scope_assertions_are_not_trusted_as_existing_relationships()
    {
        var reader = new StubReader(new Dictionary<string, string>
        {
            ["catalog_product"] = """
                DEFINE TABLE catalog_product TYPE RELATION IN catalog OUT product SCHEMAFULL;
                DEFINE FIELD tenant_id ON TABLE catalog_product TYPE int ASSERT $value != NONE OR true;
                DEFINE FIELD site_id ON TABLE catalog_product TYPE int ASSERT $value != NONE;
                """
        });
        var discovery = new SableContentRelationshipSchemaDiscovery(reader,
            new ContentPhysicalSchemaTargetRegistry([
                new("catalog-shape", "catalog"), new("product-shape", "product"), new("edge-shape", "catalog_product")]));

        (await discovery.DiscoverAsync(new ContentViewScope(3, 5))).ShouldBeEmpty();
    }

    [Test]
    public async Task Record_links_are_discovered_but_do_not_fingerprint_as_legacy_applied_schema()
    {
        var reader = new StubReader(new Dictionary<string, string>
        {
            ["product"] = "DEFINE FIELD category ON TABLE product TYPE record<category>;"
        });
        var discovery = new SableContentRelationshipSchemaDiscovery(reader,
            new ContentPhysicalSchemaTargetRegistry([new("product-shape", "product"), new("category-shape", "category")]));

        var link = (await discovery.DiscoverAsync(new ContentViewScope(3, 5))).Single();
        var legacyAppliedFingerprint = ContentRelationshipDdlLifecycle.CreateFingerprint(
            ContentRelationshipDdlLifecycle.CreateStatements(link));

        link.SchemaFingerprint.ShouldNotBe(legacyAppliedFingerprint);
        link.TargetTable.ShouldBe("category");
        link.Cardinality.ShouldBe(ContentRelationshipCardinality.ManyToOne);
    }

    [Test]
    public async Task Unregistered_physical_tables_are_not_disclosed_to_site_relationship_metadata()
    {
        var reader = new StubReader(new Dictionary<string, string>
        {
            ["secret"] = "DEFINE FIELD owner ON TABLE secret TYPE record<account>;"
        });
        var discovery = new SableContentRelationshipSchemaDiscovery(reader,
            new ContentPhysicalSchemaTargetRegistry([new("product-shape", "product")]));

        var relationships = await discovery.DiscoverAsync(new ContentViewScope(3, 5));

        relationships.ShouldBeEmpty();
    }

    private sealed class StubReader(IReadOnlyDictionary<string, string> definitions) : IContentSchemaMetadataReader
    {
        public Task<IReadOnlyDictionary<string, string>> ReadTableDefinitionsAsync(CancellationToken ct = default)
            => Task.FromResult(definitions);
    }
}
