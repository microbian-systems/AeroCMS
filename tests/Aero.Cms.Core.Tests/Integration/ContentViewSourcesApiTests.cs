using System.Net;
using System.Net.Http.Json;
using Aero.Cms.Abstractions.Content.Views;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Core.Content.Views;
using Aero.Cms.Core.Content.Services;
using Aero.Cms.Modules.Content.Areas.Api.v1;
using Aero.Core.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;

namespace Aero.Cms.Core.Tests.Integration;

public sealed class ContentViewSourcesApiTests
{
    private static readonly ContentViewScope Scope = new(41, 84);
    [Test]
    public async Task Registered_materialized_view_generates_scoped_bounded_statements_from_server_metadata()
    {
        var definition = new ContentViewSourceDefinition(
            "taxonomy_species_active",
            "wnp_taxonomy_species_active",
            OutputFieldMappings: new Dictionary<string, string>
            {
                ["external_id"] = "externalId",
                ["scientific_name"] = "scientificName"
            },
            RequiredBooleanPredicates: [new("is_active_release", true)],
            Kind: ContentViewSourceKind.MaterializedView,
            DisplayName: "Active Catalogue species",
            SuggestedShapeAlias: "species",
            IdentityField: "externalId",
            TitleField: "scientificName",
            SearchField: "scientificName");
        await using var app = await CreateAppAsync(definition,
            "DEFINE TABLE wnp_taxonomy_species_active TYPE NORMAL AS\nSELECT * FROM wnp_taxonomy_species_read WHERE is_active_release = true PERMISSIONS NONE");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/content-views/sources")
            .WithTestUser(12, role: "Admin");

        using var response = await app.GetTestClient().SendAsync(request);
        var sources = await response.Content.ReadFromJsonAsync<ContentViewSourceOption[]>();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        sources.ShouldNotBeNull();
        sources!.Length.ShouldBe(1);
        var source = sources[0];
        source.Kind.ShouldBe(ContentViewSourceKind.MaterializedView);
        source.SchemaFingerprint.Length.ShouldBe(32);
        source.ListSelectStatement.ShouldBe(
            "SELECT external_id, scientific_name FROM wnp_taxonomy_species_active WHERE tenant_id = $tenantId AND site_id = $siteId AND is_active_release = true ORDER BY scientific_name ASC, external_id ASC LIMIT 50");
        source.EntrySelectStatement.ShouldContain("external_id = $entryId LIMIT 1");
        source.SearchSelectStatement.ShouldContain("scientific_name CONTAINS $search");
    }

    [Test]
    public async Task Materialized_view_registration_is_not_offered_when_physical_source_is_an_ordinary_table()
    {
        var definition = new ContentViewSourceDefinition(
            "taxonomy_species_active",
            "wnp_taxonomy_species_active",
            Kind: ContentViewSourceKind.MaterializedView,
            IdentityField: "external_id",
            SearchField: "scientific_name");
        await using var app = await CreateAppAsync(definition,
            "DEFINE TABLE wnp_taxonomy_species_active TYPE NORMAL SCHEMAFULL PERMISSIONS NONE");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/content-views/sources")
            .WithTestUser(12, role: "Admin");

        using var response = await app.GetTestClient().SendAsync(request);
        var sources = await response.Content.ReadFromJsonAsync<ContentViewSourceOption[]>();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        sources.ShouldBeEmpty();
    }

    [Test]
    public async Task Observed_field_or_index_drift_changes_the_source_fingerprint()
    {
        var definition = new ContentViewSourceDefinition(
            "taxonomy_species_active",
            "wnp_taxonomy_species_active",
            OutputFieldMappings: new Dictionary<string, string>
            {
                ["external_id"] = "externalId",
                ["scientific_name"] = "scientificName"
            },
            Kind: ContentViewSourceKind.MaterializedView,
            IdentityField: "externalId",
            SearchField: "scientificName");
        const string table = "DEFINE TABLE wnp_taxonomy_species_active TYPE NORMAL AS SELECT * FROM wnp_taxonomy_species_read WHERE is_active_release = true PERMISSIONS NONE;";
        await using var original = await CreateAppAsync(definition,
            $"{table}\nDEFINE FIELD scientific_name ON wnp_taxonomy_species_active TYPE string;\nDEFINE INDEX idx_name ON wnp_taxonomy_species_active FIELDS scientific_name;");
        await using var changed = await CreateAppAsync(definition,
            $"{table}\nDEFINE FIELD scientific_name ON wnp_taxonomy_species_active TYPE option<string>;\nDEFINE INDEX idx_name ON wnp_taxonomy_species_active FIELDS scientific_name;");

        var originalSource = await GetOnlySourceAsync(original);
        var changedSource = await GetOnlySourceAsync(changed);

        changedSource.SchemaFingerprint.ShouldNotBe(originalSource.SchemaFingerprint);
    }

    [Test]
    public async Task Bound_save_ignores_client_statements_and_persists_current_server_snapshot()
    {
        var source = Snapshot("current");
        var snapshots = Substitute.For<IContentViewSourceSnapshotService>();
        snapshots.GetAsync(source.Alias, Arg.Any<CancellationToken>()).Returns(source);
        var service = Substitute.For<IContentSurrealViewService>();
        ContentSurrealViewRevision? captured = null;
        service.SaveDraftAsync(Arg.Any<ContentSurrealViewRevision>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                captured = call.Arg<ContentSurrealViewRevision>();
                return captured with { Version = 1 };
            });
        await using var app = await CreateManagementAppAsync(service, snapshots);
        var requestBody = new SaveContentViewDraftRequest(
            "species", "SELECT * FROM attacker LIMIT 1", "wrong", "wrong",
            "SELECT * FROM attacker LIMIT 1", "SELECT * FROM attacker LIMIT 1",
            SourceAlias: source.Alias, SourceSchemaFingerprint: source.SchemaFingerprint);
        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/admin/content-views/species/draft")
            { Content = JsonContent.Create(requestBody) };
        request.WithTestUser(12, role: "Admin");

        using var response = await app.GetTestClient().SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        captured.ShouldNotBeNull();
        captured!.SelectStatement.ShouldBe(source.ListSelectStatement);
        captured.EntrySelectStatement.ShouldBe(source.EntrySelectStatement);
        captured.SearchSelectStatement.ShouldBe(source.SearchSelectStatement);
        captured.IdentityField.ShouldBe(source.IdentityField);
        captured.SourceAlias.ShouldBe(source.Alias);
        captured.SourceSchemaFingerprint.ShouldBe(source.SchemaFingerprint);
    }

    [Test]
    public async Task Bound_save_rejects_stale_observed_schema_before_persistence()
    {
        var source = Snapshot("current");
        var snapshots = Substitute.For<IContentViewSourceSnapshotService>();
        snapshots.GetAsync(source.Alias, Arg.Any<CancellationToken>()).Returns(source);
        var service = Substitute.For<IContentSurrealViewService>();
        await using var app = await CreateManagementAppAsync(service, snapshots);
        var requestBody = new SaveContentViewDraftRequest(
            "species", "SELECT * FROM ignored LIMIT 1", "externalId", "scientificName",
            "SELECT * FROM ignored LIMIT 1", "SELECT * FROM ignored LIMIT 1",
            SourceAlias: source.Alias, SourceSchemaFingerprint: "stale");
        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/admin/content-views/species/draft")
            { Content = JsonContent.Create(requestBody) };
        request.WithTestUser(12, role: "Admin");

        using var response = await app.GetTestClient().SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await service.DidNotReceiveWithAnyArgs().SaveDraftAsync(default!, default, default);
    }

    [Test]
    public async Task Publish_rejects_schema_drift_after_a_bound_draft_was_saved()
    {
        var original = Snapshot("original");
        var current = Snapshot("current");
        var snapshots = Substitute.For<IContentViewSourceSnapshotService>();
        snapshots.GetAsync(original.Alias, Arg.Any<CancellationToken>()).Returns(current);
        var service = Substitute.For<IContentSurrealViewService>();
        service.LoadDraftAsync(Scope, "species", Arg.Any<CancellationToken>()).Returns(new ContentSurrealViewRevision(
            1, Scope, "species", "species", SpeciesShape.DefinitionValue.SchemaFingerprint,
            original.ListSelectStatement, original.IdentityField, original.TitleField,
            3, ContentViewPublicationState.Draft, DateTimeOffset.UtcNow,
            EntrySelectStatement: original.EntrySelectStatement,
            SearchSelectStatement: original.SearchSelectStatement,
            SourceAlias: original.Alias,
            SourceSchemaFingerprint: original.SchemaFingerprint));
        await using var app = await CreateManagementAppAsync(service, snapshots);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/content-views/species/publish")
            { Content = JsonContent.Create(new PublishContentViewRequest(3)) };
        request.WithTestUser(12, role: "Admin");

        using var response = await app.GetTestClient().SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await service.DidNotReceiveWithAnyArgs().PublishAsync(default, default!, default, default);
    }

    private static async Task<WebApplication> CreateAppAsync(ContentViewSourceDefinition source, string physicalDefinition)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddLogging();
        builder.Services.AddTestAuthentication();
        builder.Services.AddSingleton<IContentViewSourceRegistry>(new ContentViewSourceRegistry([new Source(source)]));
        builder.Services.AddScoped<IContentViewSourceSnapshotService, RegisteredContentViewSourceSnapshotService>();
        var metadata = Substitute.For<IContentSchemaMetadataReader>();
        metadata.ReadTableDefinitionsAsync(Arg.Any<CancellationToken>()).Returns(
            new Dictionary<string, string>(StringComparer.Ordinal) { [source.Table] = physicalDefinition });
        builder.Services.AddSingleton(metadata);
        var site = Substitute.For<ISiteContext>();
        site.TenantId.Returns(41);
        site.SiteId.Returns(84);
        builder.Services.AddSingleton(site);

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapContentViewsApi();
        await app.StartAsync();
        return app;
    }

    private static async Task<WebApplication> CreateManagementAppAsync(
        IContentSurrealViewService service,
        IContentViewSourceSnapshotService snapshots)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddLogging();
        builder.Services.AddTestAuthentication();
        builder.Services.AddSingleton(service);
        builder.Services.AddSingleton(snapshots);
        builder.Services.AddSingleton<IContentShapeRegistry>(new ContentShapeRegistry([new SpeciesShape()]));
        var site = Substitute.For<ISiteContext>();
        site.TenantId.Returns(Scope.TenantId);
        site.SiteId.Returns(Scope.SiteId);
        builder.Services.AddSingleton(site);
        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapContentViewsApi();
        await app.StartAsync();
        return app;
    }

    private static ContentViewSourceSnapshot Snapshot(string fingerprint) => new(
        "taxonomy_species_active", "Active Catalogue species", null,
        ContentViewSourceKind.MaterializedView, "wnp_taxonomy_species_active", fingerprint,
        "species", "externalId", "scientificName",
        "SELECT external_id, scientific_name FROM wnp_taxonomy_species_active WHERE tenant_id = $tenantId AND site_id = $siteId AND is_active_release = true ORDER BY scientific_name ASC, external_id ASC LIMIT 50",
        "SELECT external_id, scientific_name FROM wnp_taxonomy_species_active WHERE tenant_id = $tenantId AND site_id = $siteId AND is_active_release = true AND external_id = $entryId LIMIT 1",
        "SELECT external_id, scientific_name FROM wnp_taxonomy_species_active WHERE tenant_id = $tenantId AND site_id = $siteId AND is_active_release = true AND scientific_name CONTAINS $search ORDER BY scientific_name ASC, external_id ASC LIMIT 50");

    private static async Task<ContentViewSourceOption> GetOnlySourceAsync(WebApplication app)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/content-views/sources")
            .WithTestUser(12, role: "Admin");
        using var response = await app.GetTestClient().SendAsync(request);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var sources = await response.Content.ReadFromJsonAsync<ContentViewSourceOption[]>();
        return sources.ShouldHaveSingleItem();
    }

    private sealed class Source(ContentViewSourceDefinition definition) : IContentViewSource
    {
        public ContentViewSourceDefinition Definition { get; } = definition;
    }

    private sealed class SpeciesShape : IContentShape
    {
        public static ContentShapeDefinition DefinitionValue { get; } = Create();
        public ContentShapeDefinition Definition => DefinitionValue;

        private static ContentShapeDefinition Create()
        {
            IReadOnlyList<ContentShapeField> fields =
            [
                new("externalId", ContentShapeFieldType.String, true),
                new("scientificName", ContentShapeFieldType.String, true)
            ];
            var unsigned = new ContentShapeDefinition("species", fields, string.Empty);
            return unsigned with { SchemaFingerprint = ContentShapeFingerprint.Create(unsigned) };
        }
    }
}
