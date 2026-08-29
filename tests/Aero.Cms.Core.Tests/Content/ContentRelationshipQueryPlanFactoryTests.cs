using Aero.Cms.Abstractions.Content.Views;
using Aero.Cms.Core.Content.Views;
using Shouldly;

namespace Aero.Cms.Core.Tests.Content;

public sealed class ContentRelationshipQueryPlanFactoryTests
{
    [Test]
    public void Only_locked_applied_relationships_with_registered_sources_produce_scope_complete_candidates()
    {
        var registry = new ContentPhysicalSchemaTargetRegistry([
            new("product", "product"), new("category", "category"), new("related", "related")]);
        var sources = new ContentViewSourceRegistry([
            new Source("product"), new Source("category"), new Source("related")]);
        var factory = new ContentRelationshipQueryPlanFactory(registry, sources);
        var relationship = new ContentRelationshipDefinition(7, new(1, 2), "related", "product", "category", "product", "category", null, null, "related", ContentRelationshipKind.GraphEdge, ContentRelationshipCardinality.ManyToMany, ContentRelationshipOwnershipState.Applied, "schema-v1");

        var plan = factory.Create(relationship, new("product", "product"), new("category", "category"));

        plan.ShouldNotBeNull();
        plan!.ScopedSources.Count.ShouldBe(3);
        plan.PlanFingerprint.ShouldNotBeNullOrWhiteSpace();
        plan.SelectStatement.ShouldContain("root.tenant_id = $tenantId");
        plan.SelectStatement.ShouldContain("edge.site_id = $siteId");
        factory.Create(relationship with { OwnershipState = ContentRelationshipOwnershipState.Drifted }, new("product", "product"), new("category", "category")).ShouldBeNull();
    }

    [Test]
    public void Graph_plan_fails_closed_when_its_edge_is_not_a_registered_public_source()
    {
        var physical = new ContentPhysicalSchemaTargetRegistry([
            new("product", "product"), new("category", "category"), new("related", "related")]);
        var sources = new ContentViewSourceRegistry([new Source("product"), new Source("category")]);
        var relationship = new ContentRelationshipDefinition(7, new(1, 2), "related", "product", "category", "product", "category", null, null, "related", ContentRelationshipKind.GraphEdge, ContentRelationshipCardinality.ManyToMany, ContentRelationshipOwnershipState.Applied, "schema-v1");

        new ContentRelationshipQueryPlanFactory(physical, sources)
            .Create(relationship, new("product", "product"), new("category", "category"))
            .ShouldBeNull();
    }

    [Test]
    public void Graph_plan_includes_every_registered_visibility_predicate_in_its_identity_and_statement()
    {
        var physical = new ContentPhysicalSchemaTargetRegistry([
            new("product", "product"), new("category", "category"), new("related", "related")]);
        var root = new ContentViewSourceDefinition("product", "product",
            RequiredBooleanPredicates: [new("is_current", true)]);
        var target = new ContentViewSourceDefinition("category", "category",
            RequiredBooleanPredicates: [new("is_visible", true)]);
        var edge = new ContentViewSourceDefinition("related", "related",
            RequiredBooleanPredicates: [new("is_active", true)]);
        var sources = new ContentViewSourceRegistry([new DefinedSource(root), new DefinedSource(target), new DefinedSource(edge)]);
        var relationship = new ContentRelationshipDefinition(7, new(1, 2), "related", "product", "category", "product", "category", null, null, "related", ContentRelationshipKind.GraphEdge, ContentRelationshipCardinality.ManyToMany, ContentRelationshipOwnershipState.Applied, "schema-v1");

        var plan = new ContentRelationshipQueryPlanFactory(physical, sources).Create(relationship, root, target);

        plan.ShouldNotBeNull();
        plan!.SelectStatement.ShouldContain("root.is_current = true");
        plan.SelectStatement.ShouldContain("target.is_visible = true");
        plan.SelectStatement.ShouldContain("edge.is_active = true");
    }

    private sealed class Source(string table) : IContentViewSource
    {
        public ContentViewSourceDefinition Definition { get; } = new(table, table);
    }

    private sealed class DefinedSource(ContentViewSourceDefinition definition) : IContentViewSource
    {
        public ContentViewSourceDefinition Definition { get; } = definition;
    }
}
