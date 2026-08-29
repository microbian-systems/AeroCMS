using Aero.Cms.Abstractions.Content.Views;
using Aero.Cms.Core.Content.Views;
using Shouldly;

namespace Aero.Cms.Core.Tests.Content;

public sealed class ContentRelationshipDdlTests
{
    [Test]
    public async Task Embedded_runtime_exposes_per_statement_results_for_a_raw_create_only_transaction_probe()
    {
        await using var harness = new SableTestHarness();
        await harness.InitializeAsync();

        var response = await harness.Store.Client.RawQuery("""
            BEGIN TRANSACTION;
            CREATE ONLY content_physical_schema_claim:probe CONTENT { fingerprint: 'probe' };
            COMMIT TRANSACTION;
            """);

        response.HasErrors.ShouldBeFalse();
    }

    [Test]
    public async Task Embedded_runtime_rolls_back_raw_transaction_ddl_on_cancel()
    {
        await using var harness = new SableTestHarness();
        await harness.InitializeAsync();

        var response = await harness.Store.Client.RawQuery("""
            BEGIN TRANSACTION;
            DEFINE TABLE content_relationship_rollback_probe SCHEMAFULL;
            CANCEL TRANSACTION;
            """);
        var info = await harness.Store.Client.RawQuery("INFO FOR DB;");
        var definition = info.GetValue<System.Text.Json.JsonElement>(0).GetRawText();

        // CANCEL is reported as an error result, while the schema mutation is rolled back. This
        // proves only the primitive; it is not the complete CMS global-claim protocol.
        response.HasErrors.ShouldBeTrue();
        definition.ShouldNotContain("content_relationship_rollback_probe");
    }

    [Test]
    public async Task Ddl_uses_tables_fields_and_cardinality_without_row_population()
    {
        var scope = new ContentViewScope(1, 2);
        var oneToMany = new ContentRelationshipDefinition(1, scope, "products_categories", null, null,
            "product", "category", "categories", "id", null, ContentRelationshipKind.RecordLink,
            ContentRelationshipCardinality.OneToMany, ContentRelationshipOwnershipState.CmsDraft, string.Empty);
        var edge = new ContentRelationshipDefinition(2, scope, "related", null, null,
            "product", "category", null, null, "related", ContentRelationshipKind.GraphEdge,
            ContentRelationshipCardinality.ManyToMany, ContentRelationshipOwnershipState.CmsDraft, string.Empty);

        var recordStatements = ContentRelationshipDdlLifecycle.CreateStatements(oneToMany);
        var edgeStatements = ContentRelationshipDdlLifecycle.CreateStatements(edge);
        recordStatements.Single().ShouldBe("DEFINE FIELD categories ON TABLE product TYPE array<record<category>>;");
        edgeStatements[0].ShouldBe("DEFINE TABLE related TYPE RELATION IN product OUT category SCHEMAFULL;");
        edgeStatements.ShouldContain("DEFINE FIELD tenant_id ON TABLE related TYPE int ASSERT $value != NONE;");
        edgeStatements.ShouldContain("DEFINE FIELD site_id ON TABLE related TYPE int ASSERT $value != NONE;");
        edgeStatements.ShouldNotContain(statement => statement.Contains("RELATE ", StringComparison.OrdinalIgnoreCase));
    }

    [Test]
    public async Task Ddl_preview_produces_a_persistable_fingerprint_for_a_new_draft()
    {
        var relationship = new ContentRelationshipDefinition(1, new ContentViewScope(1, 2), "category_parent", null, null,
            "category", "category", "parent", "id", null, ContentRelationshipKind.SelfHierarchy,
            ContentRelationshipCardinality.ManyToOne, ContentRelationshipOwnershipState.CmsDraft, string.Empty);
        var lifecycle = new ContentRelationshipDdlLifecycle(new DisabledContentSchemaCommandExecutor());

        var preview = await lifecycle.PreviewAsync(relationship);
        preview.ProposedSchemaFingerprint.ShouldNotBeNullOrWhiteSpace();
        preview.Statements.Single().ShouldBe("DEFINE FIELD parent ON TABLE category TYPE option<record<category>>;");
    }

    [Test]
    public async Task Field_join_is_persistable_metadata_and_never_generates_surreal_schema_ddl()
    {
        var relationship = new ContentRelationshipDefinition(3, new ContentViewScope(1, 2), "product_category", "product-shape", "category-shape",
            "product", "categories", "category_id", "id", null, ContentRelationshipKind.FieldJoin,
            ContentRelationshipCardinality.ManyToOne, ContentRelationshipOwnershipState.CmsDraft, string.Empty);
        var lifecycle = new ContentRelationshipDdlLifecycle(new DisabledContentSchemaCommandExecutor());

        var preview = await lifecycle.PreviewAsync(relationship);

        preview.Statements.ShouldBeEmpty();
        preview.ProposedSchemaFingerprint.ShouldNotBeNullOrWhiteSpace();
    }
}
