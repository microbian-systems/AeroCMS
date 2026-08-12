using System.Net;
using System.Net.Http.Json;
using Aero.Cms.Abstractions.Content.Views;
using Aero.Cms.Abstractions.Http.Clients;
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

public sealed class ContentRelationshipDraftApiTests
{
    private static readonly ContentViewScope Scope = new(41, 84);

    [Test]
    public async Task Admin_can_save_site_scoped_relationship_draft_with_server_generated_fingerprint()
    {
        var viewService = Substitute.For<IContentSurrealViewService>();
        viewService.LoadDraftAsync(Scope, "catalog", Arg.Any<CancellationToken>())
            .Returns(new ContentSurrealViewRevision(
                1, Scope, "catalog", "catalog-shape", "shape-fingerprint",
                "SELECT id, title FROM catalog LIMIT 20", "id", "title", 1,
                ContentViewPublicationState.Draft, DateTimeOffset.UtcNow));
        var store = Substitute.For<IContentRelationshipStore>();
        store.LoadAsync(Scope, "catalog_category", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ContentRelationshipDefinition?>(null));
        store.SaveDraftAsync(Arg.Any<ContentRelationshipDefinition>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<ContentRelationshipDefinition>() with { Id = 7 });
        var lifecycle = Substitute.For<IRelationshipDdlLifecycle>();
        lifecycle.PreviewAsync(Arg.Any<ContentRelationshipDefinition>(), Arg.Any<CancellationToken>())
            .Returns(call => new RelationshipDdlPreview(
                call.Arg<ContentRelationshipDefinition>(),
                "SERVER-FINGERPRINT",
                ["DEFINE FIELD category ON TABLE catalog TYPE record<categories>;"]));
        await using var app = await CreateAppAsync(viewService, store, lifecycle);
        var body = new SaveContentRelationshipDraftRequest(
            "catalog-shape", null, "catalog", "categories", "category", null, null,
            ContentRelationshipKind.RecordLink, ContentRelationshipCardinality.ManyToOne);
        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            "/api/v1/admin/content-views/catalog/relationships/catalog_category/draft")
        {
            Content = JsonContent.Create(body)
        }.WithTestUser(12, role: "Admin");

        using var response = await app.GetTestClient().SendAsync(request);
        var saved = await response.Content.ReadFromJsonAsync<ContentRelationshipSummary>();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        saved.ShouldNotBeNull();
        saved.SchemaFingerprint.ShouldBe("SERVER-FINGERPRINT");
        saved.CanPreviewDdl.ShouldBeFalse();
        saved.CanApplyDdl.ShouldBeFalse();
        await store.Received(1).SaveDraftAsync(
            Arg.Is<ContentRelationshipDefinition>(definition =>
                definition.Scope == Scope
                && definition.OwnershipState == ContentRelationshipOwnershipState.CmsDraft
                && definition.SchemaFingerprint == "SERVER-FINGERPRINT"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Non_admin_cannot_save_relationship_draft()
    {
        var viewService = Substitute.For<IContentSurrealViewService>();
        var store = Substitute.For<IContentRelationshipStore>();
        var lifecycle = Substitute.For<IRelationshipDdlLifecycle>();
        await using var app = await CreateAppAsync(viewService, store, lifecycle);
        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            "/api/v1/admin/content-views/catalog/relationships/catalog_category/draft")
        {
            Content = JsonContent.Create(new SaveContentRelationshipDraftRequest(
                "catalog-shape", null, "catalog", "categories", "category", null, null,
                ContentRelationshipKind.RecordLink, ContentRelationshipCardinality.ManyToOne))
        }.WithTestUser(12);

        using var response = await app.GetTestClient().SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        await store.DidNotReceiveWithAnyArgs().SaveDraftAsync(default!, default);
    }

    [Test]
    public async Task New_alias_cannot_claim_existing_database_owned_physical_relationship()
    {
        var viewService = Substitute.For<IContentSurrealViewService>();
        viewService.LoadDraftAsync(Scope, "catalog", Arg.Any<CancellationToken>()).Returns(new ContentSurrealViewRevision(1, Scope, "catalog", "catalog-shape", "shape-fingerprint", "SELECT id FROM catalog LIMIT 1", "id", null, 1, ContentViewPublicationState.Draft, DateTimeOffset.UtcNow));
        var store = Substitute.For<IContentRelationshipStore>();
        var lifecycle = Substitute.For<IRelationshipDdlLifecycle>();
        var discovery = Substitute.For<IContentRelationshipSchemaDiscovery>();
        discovery.DiscoverAsync(Scope, Arg.Any<CancellationToken>()).Returns([
            new ContentRelationshipDefinition(-1, Scope, "database_link", "catalog-shape", "catalog-shape", "catalog", "categories", "category", null, null, ContentRelationshipKind.RecordLink, ContentRelationshipCardinality.ManyToOne, ContentRelationshipOwnershipState.ExternalDiscovered, "live")]);
        await using var app = await CreateAppAsync(viewService, store, lifecycle, discovery);
        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/admin/content-views/catalog/relationships/new_alias/draft")
        { Content = JsonContent.Create(new SaveContentRelationshipDraftRequest("catalog-shape", "catalog-shape", "catalog", "categories", "category", null, null, ContentRelationshipKind.RecordLink, ContentRelationshipCardinality.ManyToOne)) }.WithTestUser(12, role: "Admin");

        using var response = await app.GetTestClient().SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await store.DidNotReceive().SaveDraftAsync(Arg.Any<ContentRelationshipDefinition>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Admin_can_save_metadata_only_field_join_without_schema_ddl()
    {
        var viewService = Substitute.For<IContentSurrealViewService>();
        viewService.LoadDraftAsync(Scope, "catalog", Arg.Any<CancellationToken>()).Returns(new ContentSurrealViewRevision(1, Scope, "catalog", "catalog-shape", "shape-fingerprint", "SELECT id FROM catalog LIMIT 1", "id", null, 1, ContentViewPublicationState.Draft, DateTimeOffset.UtcNow));
        var store = Substitute.For<IContentRelationshipStore>();
        store.SaveDraftAsync(Arg.Any<ContentRelationshipDefinition>(), Arg.Any<CancellationToken>()).Returns(call => call.Arg<ContentRelationshipDefinition>() with { Id = 19 });
        var lifecycle = Substitute.For<IRelationshipDdlLifecycle>();
        lifecycle.PreviewAsync(Arg.Any<ContentRelationshipDefinition>(), Arg.Any<CancellationToken>()).Returns(call => new RelationshipDdlPreview(call.Arg<ContentRelationshipDefinition>(), "metadata-only", []));
        await using var app = await CreateAppAsync(viewService, store, lifecycle);
        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/admin/content-views/catalog/relationships/catalog_category/draft")
        { Content = JsonContent.Create(new SaveContentRelationshipDraftRequest("catalog-shape", null, "catalog", "categories", "category", "id", null, ContentRelationshipKind.FieldJoin, ContentRelationshipCardinality.ManyToOne)) }.WithTestUser(12, role: "Admin");

        using var response = await app.GetTestClient().SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var saved = await response.Content.ReadFromJsonAsync<ContentRelationshipSummary>();
        saved.ShouldNotBeNull();
        saved.CanPreviewDdl.ShouldBeFalse();
        saved.CanApplyDdl.ShouldBeFalse();
        await store.Received(1).SaveDraftAsync(Arg.Is<ContentRelationshipDefinition>(item => item.Kind == ContentRelationshipKind.FieldJoin && item.SchemaFingerprint == "metadata-only"), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Physical_relationship_preview_remains_fail_closed_even_for_admin()
    {
        var viewService = Substitute.For<IContentSurrealViewService>();
        viewService.LoadDraftAsync(Scope, "catalog", Arg.Any<CancellationToken>()).Returns(new ContentSurrealViewRevision(1, Scope, "catalog", "catalog-shape", "shape-fingerprint", "SELECT id FROM catalog LIMIT 1", "id", null, 1, ContentViewPublicationState.Draft, DateTimeOffset.UtcNow));
        var relationship = new ContentRelationshipDefinition(27, Scope, "catalog_category", "catalog-shape", "catalog-shape", "catalog", "categories", "category", null, null, ContentRelationshipKind.RecordLink, ContentRelationshipCardinality.ManyToOne, ContentRelationshipOwnershipState.CmsDraft, "fingerprint");
        var store = Substitute.For<IContentRelationshipStore>();
        store.ListAsync(Scope, Arg.Any<CancellationToken>()).Returns([relationship]);
        var lifecycle = Substitute.For<IRelationshipDdlLifecycle>();
        await using var app = await CreateAppAsync(viewService, store, lifecycle);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/content-views/catalog/relationships/27/ddl/preview")
            .WithTestUser(12, role: "Admin");

        using var response = await app.GetTestClient().SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).ShouldContain("Physical relationship schema is unavailable");
        await lifecycle.DidNotReceiveWithAnyArgs().PreviewAsync(default!, default);
    }

    private static async Task<WebApplication> CreateAppAsync(
        IContentSurrealViewService viewService,
        IContentRelationshipStore store,
        IRelationshipDdlLifecycle lifecycle,
        IContentRelationshipSchemaDiscovery? discovery = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddLogging();
        builder.Services.AddTestAuthentication();
        builder.Services.AddSingleton(viewService);
        builder.Services.AddSingleton(store);
        builder.Services.AddSingleton(lifecycle);
        if (discovery is not null) builder.Services.AddSingleton(discovery);
        builder.Services.AddSingleton<IContentShapeRegistry>(new ContentShapeRegistry([new CatalogShape()]));
        builder.Services.AddSingleton<IPrivilegedContentSchemaCommandExecutor, DisabledContentSchemaCommandExecutor>();
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

    private sealed class CatalogShape : IContentShape
    {
        public ContentShapeDefinition Definition { get; } = CreateDefinition();

        private static ContentShapeDefinition CreateDefinition()
        {
            IReadOnlyList<ContentShapeField> fields = [new ContentShapeField("id", ContentShapeFieldType.String, true)];
            var unsigned = new ContentShapeDefinition("catalog-shape", fields, string.Empty);
            return unsigned with { SchemaFingerprint = ContentShapeFingerprint.Create(unsigned) };
        }
    }
}
