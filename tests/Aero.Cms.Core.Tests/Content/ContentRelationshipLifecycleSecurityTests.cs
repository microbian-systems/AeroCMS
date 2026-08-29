using Aero.Cms.Abstractions.Content.Views;
using Aero.Cms.Core.Content.Views;
using Shouldly;

namespace Aero.Cms.Core.Tests.Content;

public sealed class ContentRelationshipLifecycleSecurityTests
{
    [Test]
    public async Task Apply_rejects_unregistered_global_tables_before_any_schema_mutation()
    {
        var coordinator = new RecordingCoordinator();
        var lifecycle = new ContentRelationshipDdlLifecycle(new EnabledExecutor(), new EmptyContentPhysicalSchemaTargetRegistry(), coordinator);
        var relationship = Draft();
        var preview = await lifecycle.PreviewAsync(relationship);

        await Should.ThrowAsync<InvalidOperationException>(() => lifecycle.ApplyAsync(preview, new ContentSchemaActor("admin")));
        coordinator.Calls.ShouldBe(0);
    }

    [Test]
    public async Task Field_join_apply_uses_actor_and_atomic_coordinator_after_registered_target_validation()
    {
        var coordinator = new RecordingCoordinator();
        var registry = new ContentPhysicalSchemaTargetRegistry([new("product-shape", "product"), new("category-shape", "category")]);
        var lifecycle = new ContentRelationshipDdlLifecycle(new EnabledExecutor(), registry, coordinator);
        var preview = await lifecycle.PreviewAsync(FieldJoin());

        var journal = await lifecycle.ApplyAsync(preview, new ContentSchemaActor("admin-42"));

        coordinator.Calls.ShouldBe(1);
        journal.AppliedBy.ShouldBe("admin-42");
    }

    [Test]
    public async Task Graph_apply_remains_disabled_even_when_a_privileged_executor_and_coordinator_are_configured()
    {
        var coordinator = new RecordingCoordinator();
        var registry = new ContentPhysicalSchemaTargetRegistry([new("product-shape", "product"), new("category-shape", "category"), new("edge", "related")]);
        var lifecycle = new ContentRelationshipDdlLifecycle(new EnabledExecutor(), registry, coordinator);
        var graph = new ContentRelationshipDefinition(8, new(1, 2), "related", "product-shape", "category-shape", "product", "category", null, null, "related", ContentRelationshipKind.GraphEdge, ContentRelationshipCardinality.ManyToMany, ContentRelationshipOwnershipState.CmsDraft, string.Empty);
        var preview = await lifecycle.PreviewAsync(graph);

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => lifecycle.ApplyAsync(preview, new ContentSchemaActor("admin")));

        exception.Message.ShouldContain("CREATE ONLY global schema claim");
        coordinator.Calls.ShouldBe(0);
    }

    [Test]
    public async Task Record_link_apply_fails_closed_without_a_host_endpoint_scope_constraint()
    {
        var coordinator = new RecordingCoordinator();
        var registry = new ContentPhysicalSchemaTargetRegistry([new("product-shape", "product"), new("category-shape", "category")]);
        var lifecycle = new ContentRelationshipDdlLifecycle(new EnabledExecutor(), registry, coordinator);
        var preview = await lifecycle.PreviewAsync(Draft());

        await Should.ThrowAsync<InvalidOperationException>(() => lifecycle.ApplyAsync(preview, new ContentSchemaActor("admin")));
        coordinator.Calls.ShouldBe(0);
    }

    [Test]
    public async Task Self_hierarchy_apply_fails_closed_without_a_host_endpoint_scope_constraint()
    {
        var coordinator = new RecordingCoordinator();
        var registry = new ContentPhysicalSchemaTargetRegistry([new("category-shape", "category")]);
        var lifecycle = new ContentRelationshipDdlLifecycle(new EnabledExecutor(), registry, coordinator);
        var hierarchy = new ContentRelationshipDefinition(9, new(1, 2), "parent", "category-shape", "category-shape", "category", "category", "parent", null, null, ContentRelationshipKind.SelfHierarchy, ContentRelationshipCardinality.ManyToOne, ContentRelationshipOwnershipState.CmsDraft, string.Empty);
        var preview = await lifecycle.PreviewAsync(hierarchy);

        await Should.ThrowAsync<InvalidOperationException>(() => lifecycle.ApplyAsync(preview, new ContentSchemaActor("admin")));
        coordinator.Calls.ShouldBe(0);
    }

    [Test]
    public void Public_capability_is_explicitly_disabled_and_has_no_configuration_switch()
    {
        var capability = new DisabledContentRelationshipSchemaCapabilityProvider().Current;

        capability.IsVerified.ShouldBeFalse();
        capability.Reason.ShouldContain("CREATE ONLY global schema claim");
    }

    private static ContentRelationshipDefinition Draft() => new(7, new ContentViewScope(1, 2), "product_category", "product-shape", "category-shape", "product", "category", "category", null, null, ContentRelationshipKind.RecordLink, ContentRelationshipCardinality.ManyToOne, ContentRelationshipOwnershipState.CmsDraft, string.Empty);

    private static ContentRelationshipDefinition FieldJoin() => Draft() with { Kind = ContentRelationshipKind.FieldJoin };

    private sealed class EnabledExecutor : IPrivilegedContentSchemaCommandExecutor
    {
        public bool IsEnabled => true;
        public Task ExecuteAsync(IReadOnlyList<string> statements, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class RecordingCoordinator : IContentRelationshipSchemaApplyCoordinator
    {
        public int Calls { get; private set; }
        public bool IsEnabled => true;
        public Task<RelationshipDdlApplyJournal> ApplyAtomicallyAsync(RelationshipDdlPreview preview, ContentSchemaActor actor, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(new RelationshipDdlApplyJournal(preview.Relationship.Id, preview.Relationship.Scope, preview.ProposedSchemaFingerprint, DateTimeOffset.UtcNow, actor.Subject));
        }
    }
}
