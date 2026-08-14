using Aero.Cms.Abstractions.Content.Views;
using Shouldly;

namespace Aero.Cms.Abstractions.Tests;

public sealed class ContentViewContractsTests
{
    [Test]
    public void Classifier_ignores_comments_and_strings_but_rejects_mutation_and_scope_omission()
    {
        var classifier = new SurrealSelectStatementClassifier();
        classifier.Classify("SELECT * FROM item WHERE tenant_id = $tenantId AND site_id = $siteId LIMIT 20 -- DELETE item").IsSingleReadOnlySelect.ShouldBeTrue();
        classifier.Classify("SELECT 'DELETE' FROM item WHERE tenant_id = $tenantId AND site_id = $siteId LIMIT 20").HasMutation.ShouldBeFalse();
        classifier.Classify("SELECT * FROM item WHERE tenant_id = $tenantId LIMIT 20").HasRequiredScopePredicates.ShouldBeFalse();
        classifier.Classify("SELECT * FROM item WHERE tenant_id = $tenantId AND site_id = $siteId LIMIT 20; DELETE item;").IsSingleReadOnlySelect.ShouldBeFalse();
    }

    [Test]
    public void Classifier_rejects_scope_predicates_outside_a_flat_top_level_where_conjunction()
    {
        var classifier = new SurrealSelectStatementClassifier();

        var projectionBypass = classifier.Classify("SELECT tenant_id = $tenantId, site_id = $siteId FROM item WHERE state = 'published' LIMIT 20");
        var orBypass = classifier.Classify("SELECT * FROM item WHERE tenant_id = $tenantId OR site_id = $siteId LIMIT 20");
        var nestedBypass = classifier.Classify("SELECT * FROM item WHERE tenant_id = $tenantId AND site_id = $siteId AND id IN (SELECT id FROM item) LIMIT 20");
        var commented = classifier.Classify("/* tenant_id = $tenantId */ SELECT 'site_id = $siteId' FROM item WHERE tenant_id = $tenantId AND site_id = $siteId LIMIT 20");

        projectionBypass.HasRequiredScopePredicates.ShouldBeFalse();
        orBypass.HasRequiredScopePredicates.ShouldBeFalse();
        nestedBypass.HasRequiredScopePredicates.ShouldBeFalse();
        // Literal projections are intentionally rejected: public output must be named source fields.
        commented.IsSingleReadOnlySelect.ShouldBeFalse();
        commented.HasRequiredScopePredicates.ShouldBeFalse();
    }

    [Test]
    public void Classifier_requires_a_bounded_terminal_limit()
    {
        var classifier = new SurrealSelectStatementClassifier();
        var missingLimit = classifier.Classify("SELECT * FROM item WHERE tenant_id = $tenantId AND site_id = $siteId");
        var trailingStart = classifier.Classify("SELECT * FROM item WHERE tenant_id = $tenantId AND site_id = $siteId LIMIT 20 START 0");

        missingLimit.IsSingleReadOnlySelect.ShouldBeFalse();
        trailingStart.IsSingleReadOnlySelect.ShouldBeFalse();
    }

    [Test]
    public void Classifier_rewrites_only_the_terminal_numeric_limit_to_a_server_owned_bound()
    {
        var classifier = new SurrealSelectStatementClassifier();
        const string statement = "SELECT id FROM item WHERE tenant_id = $tenantId AND site_id = $siteId AND title CONTAINS $search LIMIT 50";

        classifier.TryRewriteTerminalLimit(statement, 21, out var rewritten).ShouldBeTrue();
        rewritten.ShouldEndWith("LIMIT 21");
        classifier.TryRewriteTerminalLimit(statement + " START 1", 21, out _).ShouldBeFalse();
        classifier.TryRewriteTerminalLimit(statement, 101, out _).ShouldBeFalse();
    }

    [Test]
    public void Public_execution_fails_closed_without_guaranteed_read_only_executor()
    {
        var scope = new ContentViewScope(7, 11);
        var view = new ContentSurrealViewRevision(1, scope, "entries", "entry", "hash", "SELECT * FROM entry WHERE tenant_id = $tenantId AND site_id = $siteId LIMIT 10", "id", "name", 1, ContentViewPublicationState.Published, DateTimeOffset.UtcNow);
        var allowed = ContentViewExecutionPolicy.CanExecutePublicly(view, scope, new DisabledContentViewExecutor(), new SurrealSelectStatementClassifier(), new ReservedContentViewScopeBinder(), new Dictionary<string, object?>(), 10, new ContentViewExecutionLimits(), new ContentViewSourceRegistry([new Source("entry")]), out _);
        allowed.ShouldBeFalse();
    }

    [Test]
    public void Scope_binding_rejects_spoofing_and_shape_registry_exposes_safe_definitions()
    {
        var binder = new ReservedContentViewScopeBinder();
        binder.TryBind(new ContentViewScope(7, 11), new Dictionary<string, object?> { ["$siteId"] = 12 }, out _).ShouldBeFalse();
        var definition = new ContentShapeDefinition("entry", [new("nested", ContentShapeFieldType.Object, Fields: [new("name", ContentShapeFieldType.String)])], string.Empty);
        var registry = new ContentShapeRegistry([new Shape(definition with { SchemaFingerprint = ContentShapeFingerprint.Create(definition) })]);
        registry.IsValid.ShouldBeTrue();
        registry.Definitions.Count.ShouldBe(1);
    }

    [Test]
    public void Scope_binding_and_classifier_reject_mixed_case_reserved_variable_spoofing()
    {
        var binder = new ReservedContentViewScopeBinder();
        binder.TryBind(new ContentViewScope(7, 11), new Dictionary<string, object?> { ["$TenantId"] = 999 }, out _).ShouldBeFalse();
        binder.TryBind(new ContentViewScope(7, 11), new Dictionary<string, object?> { ["tenantId"] = 999 }, out _).ShouldBeFalse();
        binder.TryBind(new ContentViewScope(7, 11), new Dictionary<string, object?> { ["SITEID"] = 999 }, out _).ShouldBeFalse();
        binder.TryBind(new ContentViewScope(7, 11), new Dictionary<string, object?> { ["entryId"] = "spoof" }, out _).ShouldBeFalse();
        binder.TryBind(new ContentViewScope(7, 11), new Dictionary<string, object?> { ["Search"] = "spoof" }, out _).ShouldBeFalse();
        var classifier = new SurrealSelectStatementClassifier();
        classifier.Classify("SELECT * FROM item WHERE tenant_id = $TenantId AND site_id = $SiteId LIMIT 10").HasRequiredScopePredicates.ShouldBeFalse();
    }

    [Test]
    public void Classifier_rejects_dotted_scope_bypass_and_unregistered_join_or_graph_sources()
    {
        var classifier = new SurrealSelectStatementClassifier();
        var limits = new ContentViewExecutionLimits(20, 20);
        var sources = new ContentViewSourceRegistry([new Source("item")]);
        SurrealSelectValidator.TryGetSafeRegisteredSource("SELECT * FROM item WHERE owner.tenant_id = $tenantId AND owner.site_id = $siteId LIMIT 20", classifier, limits, sources, out _).ShouldBeFalse();
        SurrealSelectValidator.TryGetSafeRegisteredSource("SELECT * FROM item->links->category WHERE tenant_id = $tenantId AND site_id = $siteId LIMIT 20", classifier, limits, sources, out _).ShouldBeFalse();
        SurrealSelectValidator.TryGetSafeRegisteredSource("SELECT * FROM missing WHERE tenant_id = $tenantId AND site_id = $siteId LIMIT 20", classifier, limits, sources, out _).ShouldBeFalse();
    }

    [Test]
    public void Classifier_rejects_projection_dereference_and_graph_arrows_before_scope_validation()
    {
        var classifier = new SurrealSelectStatementClassifier();
        var limits = new ContentViewExecutionLimits(20, 20);
        var sources = new ContentViewSourceRegistry([new Source("item")]);

        SurrealSelectValidator.TryGetSafeRegisteredSource("SELECT ->links->category.* AS related FROM item WHERE tenant_id = $tenantId AND site_id = $siteId LIMIT 20", classifier, limits, sources, out _).ShouldBeFalse();
        SurrealSelectValidator.TryGetSafeRegisteredSource("SELECT owner.secret FROM item WHERE tenant_id = $tenantId AND site_id = $siteId LIMIT 20", classifier, limits, sources, out _).ShouldBeFalse();
    }

    [Test]
    public void Registered_source_required_boolean_predicate_is_structurally_enforced()
    {
        var classifier = new SurrealSelectStatementClassifier();
        var limits = new ContentViewExecutionLimits(20, 20);
        var sources = new ContentViewSourceRegistry([new CurrentCatalogSource()]);
        const string prefix = "SELECT id FROM catalog WHERE tenant_id = $tenantId AND site_id = $siteId";

        SurrealSelectValidator.TryGetSafeRegisteredSource(
            $"{prefix} AND is_current = true LIMIT 20", classifier, limits, sources, out _).ShouldBeTrue();
        SurrealSelectValidator.TryGetSafeRegisteredSource(
            $"{prefix} LIMIT 20", classifier, limits, sources, out _).ShouldBeFalse();
        SurrealSelectValidator.TryGetSafeRegisteredSource(
            $"{prefix} AND is_current = false LIMIT 20", classifier, limits, sources, out _).ShouldBeFalse();
        SurrealSelectValidator.TryGetSafeRegisteredSource(
            $"{prefix} AND is_current = 'true' LIMIT 20", classifier, limits, sources, out _).ShouldBeFalse();
    }

    [Test]
    public void Trusted_plan_requires_exact_code_owned_statement_and_scoped_descriptors()
    {
        const string statement = "SELECT ->links->category.* FROM item WHERE item.tenant_id = $tenantId AND item.site_id = $siteId AND links.tenant_id = $tenantId AND links.site_id = $siteId AND category.tenant_id = $tenantId AND category.site_id = $siteId LIMIT 20";
        var root = new ContentViewSourceDefinition("item", "item");
        var definition = new ContentViewTrustedQueryPlanDefinition(
            "item-categories",
            statement,
            root,
            [new("category", "category")],
            [new("links", "links")],
            [new(root, "item"), new(new("category", "category"), "category"), new(new("links", "links"), "links")]);
        var registry = new ContentViewTrustedQueryPlanRegistry([new Plan(definition)]);
        var limits = new ContentViewExecutionLimits(20, 20);

        registry.TryGet(definition.Alias, definition.PlanFingerprint, out var plan).ShouldBeTrue();
        plan!.ScopedSources.Count.ShouldBe(3);
        registry.TryGet(definition.Alias, "wrong-fingerprint", out _).ShouldBeFalse();
    }

    [Test]
    public void Virtual_parameters_must_appear_in_executable_predicates()
    {
        IContentViewStatementClassifier classifier = new SurrealSelectStatementClassifier();
        const string exact = "SELECT id FROM item WHERE tenant_id = $tenantId AND site_id = $siteId AND id = 'fixed' /* id = $entryId */ LIMIT 1";
        const string search = "SELECT '$search' AS note FROM item WHERE tenant_id = $tenantId AND site_id = $siteId AND title = 'fixed' LIMIT 20";
        const string validExact = "SELECT id FROM item WHERE tenant_id = $tenantId AND site_id = $siteId AND id = $entryId LIMIT 1";
        const string validSearch = "SELECT id FROM item WHERE tenant_id = $tenantId AND site_id = $siteId AND title CONTAINS $search LIMIT 20";

        SurrealSelectValidator.HasRequiredBoundEquality(exact, "id", "$entryId", classifier).ShouldBeFalse();
        SurrealSelectValidator.HasRequiredBoundParameter(search, "$search", classifier).ShouldBeFalse();
        SurrealSelectValidator.HasRequiredBoundEquality(validExact, "id", "$entryId", classifier).ShouldBeTrue();
        SurrealSelectValidator.HasRequiredBoundParameter(validSearch, "$search", classifier).ShouldBeTrue();
    }

    [Test]
    public void Public_plan_fingerprint_includes_required_visibility_predicates()
    {
        var visible = new ContentViewSourceDefinition("catalog", "catalog",
            RequiredBooleanPredicates: [new("is_current", true)]);
        var hidden = visible with
        {
            RequiredBooleanPredicates = [new("is_current", false)]
        };
        var visiblePlan = new ContentViewTrustedQueryPlanDefinition(
            "catalog-plan", "SELECT * FROM catalog WHERE tenant_id = $tenantId AND site_id = $siteId AND is_current = true LIMIT 20", visible, [], []);
        var hiddenPlan = visiblePlan with { RootSource = hidden };

        visiblePlan.PlanFingerprint.ShouldNotBe(hiddenPlan.PlanFingerprint);
    }

    [Test]
    public void Registry_rejects_fingerprint_drift_duplicate_nested_names_and_unresolved_references()
    {
        var drift = new ContentShapeDefinition("entry", [new("name", ContentShapeFieldType.String)], "not-canonical");
        var duplicateNested = new ContentShapeDefinition("nested", [new("value", ContentShapeFieldType.Object, Fields: [new("x", ContentShapeFieldType.String), new("x", ContentShapeFieldType.String)])], string.Empty);
        duplicateNested = duplicateNested with { SchemaFingerprint = ContentShapeFingerprint.Create(duplicateNested) };
        var unresolved = new ContentShapeDefinition("ref", [new("related", ContentShapeFieldType.Reference, ReferenceShapeAlias: "missing")], string.Empty);
        unresolved = unresolved with { SchemaFingerprint = ContentShapeFingerprint.Create(unresolved) };
        var registry = new ContentShapeRegistry([new Shape(drift), new Shape(duplicateNested), new Shape(unresolved)]);
        registry.IsValid.ShouldBeFalse();
        registry.Errors.Count.ShouldBeGreaterThan(2);
    }

    [Test]
    public void Relationship_lifecycle_locks_applied_and_drifted_graph_links()
    {
        var relationship = new ContentRelationshipDefinition(1, new ContentViewScope(7, 11), "links", "item", "category", "item", "category", null, null, "links", ContentRelationshipKind.GraphEdge, ContentRelationshipCardinality.ManyToMany, ContentRelationshipOwnershipState.Applied, "fingerprint");
        relationship.IsMutationBlocked.ShouldBeTrue();
        (relationship with { OwnershipState = ContentRelationshipOwnershipState.Drifted }).IsMutationBlocked.ShouldBeTrue();
    }

    private sealed class Shape(ContentShapeDefinition definition) : IContentShape { public ContentShapeDefinition Definition { get; } = definition; }
    private sealed class Source(string table) : IContentViewSource { public ContentViewSourceDefinition Definition { get; } = new(table, table); }
    private sealed class CurrentCatalogSource : IContentViewSource
    {
        public ContentViewSourceDefinition Definition { get; } = new(
            "catalog",
            "catalog",
            RequiredBooleanPredicates: [new("is_current", true)]);
    }
    private sealed class Plan(ContentViewTrustedQueryPlanDefinition definition) : IContentViewTrustedQueryPlan { public ContentViewTrustedQueryPlanDefinition Definition { get; } = definition; }
}
