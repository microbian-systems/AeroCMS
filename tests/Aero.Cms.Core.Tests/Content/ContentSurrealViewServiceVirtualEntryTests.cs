using Aero.Cms.Abstractions.Content.Views;
using Aero.Cms.Core.Content.Services;
using Aero.Cms.Core.Content.Views;
using Shouldly;
using System.Text.Json;

namespace Aero.Cms.Core.Tests.Content;

public sealed class ContentSurrealViewServiceVirtualEntryTests
{
    [Test]
    public async Task Administrator_preview_allows_registered_scoped_source_but_draft_remains_ineligible_for_publication()
    {
        var scope = new ContentViewScope(1, 2);
        var draft = new ContentSurrealViewRevision(3, scope, "catalog", "catalog", "unused",
            "SELECT * FROM catalog WHERE tenant_id = $tenantId AND site_id = $siteId LIMIT 10",
            "id", null, 1, ContentViewPublicationState.Draft, DateTimeOffset.UtcNow);
        var administrator = new RecordingAdministratorExecutor();
        var service = new ContentSurrealViewService(new EmptyStore(), new EmptyInvalidator(), new RecordingExecutor(),
            new SurrealSelectStatementClassifier(), new ReservedContentViewScopeBinder(), new CatalogShapes(), new CatalogSources(),
            new EmptyCache(), new EmptyGeneration(), administrator);

        var saved = await service.SaveDraftAsync(draft, 100);
        var preview = await service.PreviewAsync(draft, scope, new Dictionary<string, object?>(), 1, 10);

        saved.ShouldNotBeNull();
        saved.PublicExecutionEligible.ShouldBeFalse();
        saved.PublicExecutionIneligibilityReason.ShouldNotBeNull();
        preview.ShouldNotBeNull();
        administrator.Parameters!["$tenantId"].ShouldBe(scope.TenantId);
        administrator.Parameters["$siteId"].ShouldBe(scope.SiteId);
    }

    [Test]
    public void Administrator_preview_rejects_mutation_and_mixed_case_scope_variables()
    {
        var classifier = new SurrealSelectStatementClassifier();

        classifier.IsSingleReadOnlySelect("SELECT * FROM catalog WHERE tenant_id = $TenantId AND site_id = $siteId LIMIT 1").ShouldBeFalse();
        classifier.IsSingleReadOnlySelect("SELECT $tenantId, $siteId FROM catalog WHERE tenant_id != $tenantId OR site_id != $siteId LIMIT 1").ShouldBeFalse();
        classifier.IsSingleReadOnlySelect("SELECT * FROM catalog WHERE 1 = $tenantId AND 2 = $siteId LIMIT 1").ShouldBeFalse();
        classifier.IsSingleReadOnlySelect("SELECT * FROM catalog WHERE tenant_id = $tenantId AND site_id = $siteId; DELETE catalog").ShouldBeFalse();
    }

    [Test]
    public async Task Administrator_preview_rejects_unregistered_or_wrong_scope_fields()
    {
        var scope = new ContentViewScope(1, 2);
        var administrator = new RecordingAdministratorExecutor();
        var service = new ContentSurrealViewService(new EmptyStore(), new EmptyInvalidator(), new RecordingExecutor(),
            new SurrealSelectStatementClassifier(), new ReservedContentViewScopeBinder(), new CatalogShapes(), new CatalogSources(),
            new EmptyCache(), new EmptyGeneration(), administrator);
        var unregistered = new ContentSurrealViewRevision(3, scope, "catalog", "catalog", "unused",
            "SELECT * FROM private_records WHERE tenant_id = $tenantId AND site_id = $siteId LIMIT 10",
            "id", null, 1, ContentViewPublicationState.Draft, DateTimeOffset.UtcNow);
        var wrongFields = unregistered with
        {
            SelectStatement = "SELECT * FROM catalog WHERE owner_tenant = $tenantId AND owner_site = $siteId LIMIT 10"
        };

        (await service.PreviewAsync(unregistered, scope, new Dictionary<string, object?>(), 1, 10)).ShouldBeNull();
        (await service.PreviewAsync(wrongFields, scope, new Dictionary<string, object?>(), 1, 10)).ShouldBeNull();
        administrator.Parameters.ShouldBeNull();
    }

    [Test]
    public async Task Administrator_preview_projects_registered_physical_fields_to_the_code_owned_shape()
    {
        var scope = new ContentViewScope(1, 2);
        var draft = new ContentSurrealViewRevision(3, scope, "catalog", "catalog-entry", "shape",
            "SELECT external_id FROM catalog_read WHERE tenant_id = $tenantId AND site_id = $siteId LIMIT 10",
            "externalId", null, 1, ContentViewPublicationState.Draft, DateTimeOffset.UtcNow);
        var administrator = new RecordingAdministratorExecutor(
            [new Dictionary<string, object?> { ["external_id"] = "entry-1", ["private_note"] = "not public" }]);
        var service = new ContentSurrealViewService(new EmptyStore(), new EmptyInvalidator(), new RecordingExecutor(),
            new SurrealSelectStatementClassifier(), new ReservedContentViewScopeBinder(), new MappedShapes(), new MappedSources(),
            new EmptyCache(), new EmptyGeneration(), administrator);

        var preview = await service.PreviewAsync(draft, scope, new Dictionary<string, object?>(), 1, 10);

        preview.ShouldNotBeNull();
        preview.Rows.ShouldHaveSingleItem();
        preview.Rows[0].Keys.ShouldBe(["externalId"]);
        preview.Rows[0]["externalId"].ShouldBe("entry-1");
    }

    [Test]
    public async Task Exact_entry_does_not_revalidate_the_search_query_at_one_row()
    {
        var scope = new ContentViewScope(1, 2);
        var view = new ContentSurrealViewRevision(1, scope, "catalog", "catalog", "shape",
            "SELECT * FROM catalog WHERE tenant_id = $tenantId AND site_id = $siteId LIMIT 50", "id", "name", 1,
            ContentViewPublicationState.Published, DateTimeOffset.UtcNow, EntrySelectStatement:
            "SELECT * FROM catalog WHERE tenant_id = $tenantId AND site_id = $siteId AND id = $entryId LIMIT 1",
            SearchSelectStatement: "SELECT * FROM catalog WHERE tenant_id = $tenantId AND site_id = $siteId AND name CONTAINS $search LIMIT 50");
        var executor = new RecordingExecutor();
        var service = new ContentSurrealViewService(new EmptyStore(), new EmptyInvalidator(), executor,
            new SurrealSelectStatementClassifier(), new ReservedContentViewScopeBinder(), new CatalogShapes(), new CatalogSources(), new EmptyCache(), new EmptyGeneration());

        var result = await service.ExecuteEntryAsync(view, scope, "entry-1");

        result.ShouldNotBeNull();
        executor.LastRequest!.Take.ShouldBe(1);
        executor.LastRequest.Parameters["$entryId"].ShouldBe("entry-1");
    }

    [Test]
    public async Task Search_rewrites_the_saved_terminal_limit_to_the_server_requested_page_window()
    {
        var scope = new ContentViewScope(1, 2);
        var view = new ContentSurrealViewRevision(1, scope, "catalog", "catalog", "shape",
            "SELECT * FROM catalog WHERE tenant_id = $tenantId AND site_id = $siteId LIMIT 50", "id", null, 1,
            ContentViewPublicationState.Published, DateTimeOffset.UtcNow,
            EntrySelectStatement: "SELECT * FROM catalog WHERE tenant_id = $tenantId AND site_id = $siteId AND id = $entryId LIMIT 1",
            SearchSelectStatement: "SELECT * FROM catalog WHERE tenant_id = $tenantId AND site_id = $siteId AND id CONTAINS $search LIMIT 50");
        var executor = new RecordingExecutor();
        var service = new ContentSurrealViewService(new EmptyStore(), new EmptyInvalidator(), executor,
            new SurrealSelectStatementClassifier(), new ReservedContentViewScopeBinder(), new CatalogShapes(), new CatalogSources(),
            new EmptyCache(), new EmptyGeneration());

        var result = await service.SearchEntriesAsync(view, scope, "entry", 21);

        result.ShouldNotBeNull();
        executor.LastRequest.ShouldNotBeNull();
        executor.LastRequest.Take.ShouldBe(21);
        executor.LastRequest.View.SelectStatement.ShouldEndWith("LIMIT 21");
        executor.LastRequest.Limits.MaximumRows.ShouldBe(21);
    }

    [Test]
    public async Task Search_allows_an_empty_filter_for_an_unfiltered_virtual_list()
    {
        var scope = new ContentViewScope(1, 2);
        var view = new ContentSurrealViewRevision(1, scope, "catalog", "catalog", "shape",
            "SELECT * FROM catalog WHERE tenant_id = $tenantId AND site_id = $siteId LIMIT 50", "id", null, 1,
            ContentViewPublicationState.Published, DateTimeOffset.UtcNow,
            EntrySelectStatement: "SELECT * FROM catalog WHERE tenant_id = $tenantId AND site_id = $siteId AND id = $entryId LIMIT 1",
            SearchSelectStatement: "SELECT * FROM catalog WHERE tenant_id = $tenantId AND site_id = $siteId AND id CONTAINS $search LIMIT 50");
        var executor = new RecordingExecutor();
        var service = new ContentSurrealViewService(new EmptyStore(), new EmptyInvalidator(), executor,
            new SurrealSelectStatementClassifier(), new ReservedContentViewScopeBinder(), new CatalogShapes(), new CatalogSources(),
            new EmptyCache(), new EmptyGeneration());

        var result = await service.SearchEntriesAsync(view, scope, string.Empty, 20);

        result.ShouldNotBeNull();
        executor.LastRequest.ShouldNotBeNull();
        executor.LastRequest.Parameters["$search"].ShouldBe(string.Empty);
    }

    [Test]
    public async Task Provider_lists_the_published_view_when_no_search_filter_is_supplied()
    {
        var scope = new ContentViewScope(1, 2);
        var view = new ContentSurrealViewRevision(1, scope, "catalog", "catalog", "shape",
            "SELECT * FROM catalog WHERE tenant_id = $tenantId AND site_id = $siteId LIMIT 50", "id", null, 1,
            ContentViewPublicationState.Published, DateTimeOffset.UtcNow,
            EntrySelectStatement: "SELECT * FROM catalog WHERE tenant_id = $tenantId AND site_id = $siteId AND id = $entryId LIMIT 1",
            SearchSelectStatement: "SELECT * FROM catalog WHERE tenant_id = $tenantId AND site_id = $siteId AND id CONTAINS $search LIMIT 50");
        var executor = new RecordingExecutor();
        var service = new ContentSurrealViewService(new PublishedStore(view), new EmptyInvalidator(), executor,
            new SurrealSelectStatementClassifier(), new ReservedContentViewScopeBinder(), new CatalogShapes(), new CatalogSources(),
            new EmptyCache(), new EmptyGeneration());
        var provider = new ContentSurrealViewEntryProvider(view, service);

        var result = await provider.SearchAsync(scope, "en-US", null, 20);

        result.ShouldHaveSingleItem().Key.StableId.ShouldBe("entry-1");
        executor.LastRequest.ShouldNotBeNull();
        executor.LastRequest.View.SelectStatement.ShouldBe(view.SelectStatement);
        executor.LastRequest.Parameters.ShouldNotContainKey("$search");
    }

    [Test]
    public async Task Exact_entry_rejects_a_row_whose_identity_does_not_match_the_requested_key()
    {
        var scope = new ContentViewScope(1, 2);
        var view = new ContentSurrealViewRevision(1, scope, "catalog", "catalog", "shape",
            "SELECT * FROM catalog WHERE tenant_id = $tenantId AND site_id = $siteId LIMIT 50", "id", "name", 1,
            ContentViewPublicationState.Published, DateTimeOffset.UtcNow, EntrySelectStatement:
            "SELECT * FROM catalog WHERE tenant_id = $tenantId AND site_id = $siteId AND id = $entryId LIMIT 1",
            SearchSelectStatement: "SELECT * FROM catalog WHERE tenant_id = $tenantId AND site_id = $siteId AND name CONTAINS $search LIMIT 50");
        var service = new ContentSurrealViewService(new EmptyStore(), new EmptyInvalidator(), new RecordingExecutor(),
            new SurrealSelectStatementClassifier(), new ReservedContentViewScopeBinder(), new CatalogShapes(), new CatalogSources(), new EmptyCache(), new EmptyGeneration());
        var provider = new ContentSurrealViewEntryProvider(view, service);

        var result = await provider.FindAsync(scope, "entry:missing");

        result.ShouldBeNull();
    }

    [Test]
    public async Task Exact_entry_miss_is_not_cached_so_a_later_record_is_visible_without_waiting_for_ttl()
    {
        var scope = new ContentViewScope(1, 2);
        var view = new ContentSurrealViewRevision(1, scope, "catalog", "catalog", "shape",
            "SELECT * FROM catalog WHERE tenant_id = $tenantId AND site_id = $siteId LIMIT 50", "id", null, 1,
            ContentViewPublicationState.Published, DateTimeOffset.UtcNow,
            EntrySelectStatement: "SELECT * FROM catalog WHERE tenant_id = $tenantId AND site_id = $siteId AND id = $entryId LIMIT 1",
            SearchSelectStatement: "SELECT * FROM catalog WHERE tenant_id = $tenantId AND site_id = $siteId AND id CONTAINS $search LIMIT 50");
        var executor = new RecordingExecutor([]);
        var cache = new RecordingCache();
        var service = new ContentSurrealViewService(new EmptyStore(), new EmptyInvalidator(), executor,
            new SurrealSelectStatementClassifier(), new ReservedContentViewScopeBinder(), new CatalogShapes(), new CatalogSources(), cache, new EmptyGeneration());

        (await service.ExecuteEntryAsync(view, scope, "not-yet-created"))!.Rows.ShouldBeEmpty();
        executor.LastRequest.ShouldNotBeNull();
        cache.SetCalls.ShouldBe(0);

        await service.ExecuteEntryAsync(view, scope, "not-yet-created");
        cache.SetCalls.ShouldBe(0);
    }

    [Test]
    public async Task Independent_generation_tokens_do_not_overflow_cache_identity()
    {
        var scope = new ContentViewScope(1, 2);
        var view = new ContentSurrealViewRevision(1, scope, "catalog", "catalog", "shape",
            "SELECT * FROM catalog WHERE tenant_id = $tenantId AND site_id = $siteId LIMIT 1", "id", null, 1,
            ContentViewPublicationState.Published, DateTimeOffset.UtcNow, CacheGeneration: long.MaxValue);
        var service = new ContentSurrealViewService(new EmptyStore(), new EmptyInvalidator(), new RecordingExecutor(),
            new SurrealSelectStatementClassifier(), new ReservedContentViewScopeBinder(), new CatalogShapes(), new CatalogSources(),
            new RecordingCache(), new FixedGeneration(long.MaxValue), distributedCacheCoordinator: new FixedDistributedGeneration(long.MaxValue));

        var result = await service.ExecutePublicAsync(scope, view.Alias, new Dictionary<string, object?>(), 1);

        // EmptyStore has no persisted revision; exercise the same identity path through the virtual entry call.
        result.ShouldBeNull();
        (await service.ExecuteEntryAsync(view with
        {
            EntrySelectStatement = "SELECT * FROM catalog WHERE tenant_id = $tenantId AND site_id = $siteId AND id = $entryId LIMIT 1"
        }, scope, "entry-1")).ShouldNotBeNull();
    }

    [Test]
    public async Task Exact_entry_maps_real_transport_json_string_identity_and_title()
    {
        var scope = new ContentViewScope(1, 2);
        var view = new ContentSurrealViewRevision(1, scope, "catalog", "catalog", "shape",
            "SELECT id FROM catalog WHERE tenant_id = $tenantId AND site_id = $siteId LIMIT 50", "id", "name", 1,
            ContentViewPublicationState.Published, DateTimeOffset.UtcNow, EntrySelectStatement:
            "SELECT id,name FROM catalog WHERE tenant_id = $tenantId AND site_id = $siteId AND id = $entryId LIMIT 1",
            SearchSelectStatement: "SELECT id,name FROM catalog WHERE tenant_id = $tenantId AND site_id = $siteId AND name CONTAINS $search LIMIT 50");
        using var json = JsonDocument.Parse("{\"id\":\"entry-1\",\"name\":\"Sample entry\"}");
        var row = json.RootElement.EnumerateObject().ToDictionary(
            property => property.Name,
            property => (object?)property.Value.Clone(),
            StringComparer.Ordinal);
        var service = new ContentSurrealViewService(new EmptyStore(), new EmptyInvalidator(), new RecordingExecutor([row]),
            new SurrealSelectStatementClassifier(), new ReservedContentViewScopeBinder(), new TitledCatalogShapes(), new CatalogSources(),
            new EmptyCache(), new EmptyGeneration());
        var provider = new ContentSurrealViewEntryProvider(view, service);

        var result = await provider.FindAsync(scope, "entry-1");

        result.ShouldNotBeNull();
        result.Key.StableId.ShouldBe("entry-1");
        result.Values["id"].ShouldBe("entry-1");
        result.Values["title"].ShouldBe("Sample entry");
    }

    [Test]
    public void Published_entry_validation_maps_shape_identity_to_physical_field_and_requires_source_visibility_predicate()
    {
        var scope = new ContentViewScope(1, 2);
        var view = new ContentSurrealViewRevision(1, scope, "catalog", "catalog-entry", "shape",
            "SELECT external_id FROM catalog_read WHERE tenant_id = $tenantId AND site_id = $siteId AND is_current = true LIMIT 50",
            "externalId", null, 1, ContentViewPublicationState.Published, DateTimeOffset.UtcNow,
            EntrySelectStatement: "SELECT external_id FROM catalog_read WHERE tenant_id = $tenantId AND site_id = $siteId AND is_current = true AND external_id = $entryId LIMIT 1",
            SearchSelectStatement: "SELECT external_id FROM catalog_read WHERE tenant_id = $tenantId AND site_id = $siteId AND is_current = true AND external_id CONTAINS $search LIMIT 50");
        var service = new ContentSurrealViewService(new EmptyStore(), new EmptyInvalidator(), new RecordingExecutor(),
            new SurrealSelectStatementClassifier(), new ReservedContentViewScopeBinder(), new MappedShapes(), new MappedSources(),
            new EmptyCache(), new EmptyGeneration());

        service.ValidatePublishedEntryStatements(view, 50).ShouldBeTrue();
        service.ValidatePublishedEntryStatements(view with
        {
            EntrySelectStatement = view.EntrySelectStatement!.Replace(" AND is_current = true", string.Empty, StringComparison.Ordinal)
        }, 50).ShouldBeFalse();
    }

    private sealed class RecordingExecutor : IReadOnlyContentViewExecutor
    {
        private readonly IReadOnlyList<IReadOnlyDictionary<string, object?>> _rows;
        public RecordingExecutor(IReadOnlyList<IReadOnlyDictionary<string, object?>>? rows = null)
            => _rows = rows ?? [new Dictionary<string, object?> { ["id"] = "entry-1" }];
        public bool IsReadOnlyGuaranteed => true;
        public ContentViewExecutionRequest? LastRequest { get; private set; }
        public Task<ContentViewExecutionResult> ExecuteAsync(ContentViewExecutionRequest request, CancellationToken ct = default)
        {
            LastRequest = request;
            return Task.FromResult(new ContentViewExecutionResult(_rows, false));
        }
    }
    private sealed class RecordingAdministratorExecutor : IAdminReadOnlyContentViewExecutor
    {
        private readonly IReadOnlyList<IReadOnlyDictionary<string, object?>> _rows;
        public RecordingAdministratorExecutor(IReadOnlyList<IReadOnlyDictionary<string, object?>>? rows = null)
            => _rows = rows ?? [new Dictionary<string, object?> { ["id"] = "entry-1" }];
        public bool IsReadOnlyGuaranteed => true;
        public IReadOnlyDictionary<string, object?>? Parameters { get; private set; }
        public Task<ContentViewExecutionResult> ExecuteAsync(ContentSurrealViewRevision view, ContentViewScope scope,
            IReadOnlyDictionary<string, object?> parameters, ContentViewExecutionLimits limits, CancellationToken ct = default)
        {
            Parameters = parameters;
            return Task.FromResult(new ContentViewExecutionResult(_rows, false));
        }
    }
    private sealed class EmptyStore : IContentSurrealViewStore
    {
        public Task<ContentSurrealViewRevision?> LoadAsync(ContentViewScope scope, string alias, ContentViewPublicationState state, CancellationToken ct = default) => Task.FromResult<ContentSurrealViewRevision?>(null);
        public Task<IReadOnlyList<ContentSurrealViewRevision>> ListPublishedAsync(ContentViewScope scope, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ContentSurrealViewRevision>>([]);
        public Task<ContentSurrealViewRevision> SaveDraftAsync(ContentSurrealViewRevision draft, CancellationToken ct = default) => Task.FromResult(draft);
        public Task<ContentSurrealViewRevision?> PublishAsync(ContentViewScope scope, string alias, long draftVersion, CancellationToken ct = default) => Task.FromResult<ContentSurrealViewRevision?>(null);
    }
    private sealed class PublishedStore(ContentSurrealViewRevision view) : IContentSurrealViewStore
    {
        public Task<ContentSurrealViewRevision?> LoadAsync(ContentViewScope scope, string alias, ContentViewPublicationState state, CancellationToken ct = default)
            => Task.FromResult<ContentSurrealViewRevision?>(scope == view.Scope && alias == view.Alias && state == ContentViewPublicationState.Published ? view : null);
        public Task<IReadOnlyList<ContentSurrealViewRevision>> ListPublishedAsync(ContentViewScope scope, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ContentSurrealViewRevision>>(scope == view.Scope ? [view] : []);
        public Task<ContentSurrealViewRevision> SaveDraftAsync(ContentSurrealViewRevision draft, CancellationToken ct = default) => Task.FromResult(draft);
        public Task<ContentSurrealViewRevision?> PublishAsync(ContentViewScope scope, string alias, long draftVersion, CancellationToken ct = default) => Task.FromResult<ContentSurrealViewRevision?>(null);
    }
    private sealed class EmptyInvalidator : IContentViewCacheInvalidator { public Task InvalidateAsync(ContentViewScope scope, CancellationToken ct = default) => Task.CompletedTask; }
    private sealed class EmptyCache : IContentViewExecutionCache { public Task<ContentViewExecutionResult?> TryGetAsync(string key, CancellationToken ct = default) => Task.FromResult<ContentViewExecutionResult?>(null); public Task SetAsync(string key, ContentViewExecutionResult value, TimeSpan duration, CancellationToken ct = default) => Task.CompletedTask; public Task InvalidateAsync(ContentViewScope scope, CancellationToken ct = default) => Task.CompletedTask; }
    private sealed class EmptyGeneration : IContentViewCacheGenerationProvider { public Task<long> GetGenerationAsync(ContentViewScope scope, CancellationToken ct = default) => Task.FromResult(0L); }
    private sealed class FixedGeneration(long value) : IContentViewCacheGenerationProvider { public Task<long> GetGenerationAsync(ContentViewScope scope, CancellationToken ct = default) => Task.FromResult(value); }
    private sealed class FixedDistributedGeneration(long value) : IContentViewDistributedCacheCoordinator
    {
        public bool IsDistributed => true;
        public Task<long> GetGenerationAsync(ContentViewScope scope, CancellationToken ct = default) => Task.FromResult(value);
        public Task InvalidateAsync(ContentViewScope scope, CancellationToken ct = default) => Task.CompletedTask;
    }
    private sealed class RecordingCache : IContentViewExecutionCache
    {
        public int SetCalls { get; private set; }
        public Task<ContentViewExecutionResult?> TryGetAsync(string key, CancellationToken ct = default) => Task.FromResult<ContentViewExecutionResult?>(null);
        public Task SetAsync(string key, ContentViewExecutionResult value, TimeSpan duration, CancellationToken ct = default) { SetCalls++; return Task.CompletedTask; }
        public Task InvalidateAsync(ContentViewScope scope, CancellationToken ct = default) => Task.CompletedTask;
    }
    private sealed class CatalogShapes : IContentShapeRegistry
    {
        private static readonly ContentShapeDefinition Definition = new("catalog", [new("id", ContentShapeFieldType.String, Required: true)], "unused");
        public bool IsValid => true; public IReadOnlyList<string> Errors => []; public IReadOnlyList<ContentShapeDefinition> Definitions => [Definition];
        public bool TryGet(string alias, out ContentShapeDefinition? definition) { definition = alias == "catalog" ? Definition : null; return definition is not null; }
    }
    private sealed class TitledCatalogShapes : IContentShapeRegistry
    {
        private static readonly ContentShapeDefinition Definition = new("catalog",
            [new("id", ContentShapeFieldType.String, Required: true), new("name", ContentShapeFieldType.String)], "shape");
        public bool IsValid => true; public IReadOnlyList<string> Errors => []; public IReadOnlyList<ContentShapeDefinition> Definitions => [Definition];
        public bool TryGet(string alias, out ContentShapeDefinition? definition) { definition = alias == "catalog" ? Definition : null; return definition is not null; }
    }
    private sealed class CatalogSources : IContentViewSourceRegistry
    {
        private static readonly ContentViewSourceDefinition Definition = new("catalog", "catalog");
        public bool IsValid => true; public bool HasSources => true; public IReadOnlyList<string> Errors => [];
        public bool TryGetByTable(string table, out ContentViewSourceDefinition? source) { source = table == "catalog" ? Definition : null; return source is not null; }
    }
    private sealed class MappedShapes : IContentShapeRegistry
    {
        private static readonly ContentShapeDefinition Definition = new("catalog-entry", [new("externalId", ContentShapeFieldType.String, Required: true)], "shape");
        public bool IsValid => true; public IReadOnlyList<string> Errors => []; public IReadOnlyList<ContentShapeDefinition> Definitions => [Definition];
        public bool TryGet(string alias, out ContentShapeDefinition? definition) { definition = alias == "catalog-entry" ? Definition : null; return definition is not null; }
    }
    private sealed class MappedSources : IContentViewSourceRegistry
    {
        private static readonly ContentViewSourceDefinition Definition = new(
            "catalog-entry",
            "catalog_read",
            OutputFieldMappings: new Dictionary<string, string> { ["external_id"] = "externalId" },
            RequiredBooleanPredicates: [new("is_current", true)]);
        public bool IsValid => true; public bool HasSources => true; public IReadOnlyList<string> Errors => [];
        public bool TryGetByTable(string table, out ContentViewSourceDefinition? source) { source = table == "catalog_read" ? Definition : null; return source is not null; }
    }
}
