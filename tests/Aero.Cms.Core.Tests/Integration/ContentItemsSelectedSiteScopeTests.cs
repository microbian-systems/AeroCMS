using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Content.Views;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Core.Content.Services;
using Aero.Cms.Core.Infrastructure;
using Aero.Cms.Modules.Content.Areas.Api.v1;
using Aero.Core;
using Aero.Core.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;

namespace Aero.Cms.Core.Tests.Integration;

public sealed class ContentItemsSelectedSiteScopeTests
{
    private static readonly ContentViewScope Scope = new(41, 8311);

    [Test]
    public async Task Provider_picker_uses_persisted_tenant_for_manager_selected_site()
    {
        var provider = Substitute.For<IContentEntrySourceProvider>();
        provider.Provider.Returns("registry");
        var catalog = Substitute.For<IContentEntrySourceProviderCatalog>();
        catalog.ListProviderKeysAsync(Scope, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<string>>(["view:sample"]));
        var resolver = CreateResolver();
        await using var app = await CreateAppAsync(
            Substitute.For<IAeroContentItemActor>(),
            provider,
            catalog,
            resolver);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/v1/admin/content-items/entry-reference-sources").WithTestUser(12);

        using var response = await app.GetTestClient().SendAsync(request);
        var sources = await response.Content.ReadFromJsonAsync<IReadOnlyList<CmsContentReferenceSource>>();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        sources.ShouldNotBeNull();
        sources.Select(source => source.Key).ShouldBe(["registry", "view:sample"]);
        await resolver.Received(1).ResolveAsync(Scope.SiteId, Arg.Any<CancellationToken>());
        await catalog.Received(1).ListProviderKeysAsync(Scope, Arg.Any<CancellationToken>());
    }

    [Test]
    [Arguments("create")]
    [Arguments("update")]
    public async Task Virtual_reference_mutation_uses_resolved_manager_site_scope(string operation)
    {
        const long itemId = 10;
        var actor = Substitute.For<IAeroContentItemActor>();
        var stored = SuccessfulItem(itemId);
        actor.GetByIdAsync(itemId, Scope.SiteId, Arg.Any<CancellationToken>()).Returns(stored);
        actor.SaveDraftAsync(Arg.Any<ContentItemViewModel>(), Scope.SiteId, Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var value = call.Arg<ContentItemViewModel>();
                value.Id = itemId;
                return new AeroRequestResponse<ContentItemViewModel>(value, new ContentItemErrorViewModel());
            });
        var resolver = CreateResolver();
        await using var app = await CreateAppAsync(
            actor,
            Substitute.For<IContentEntrySourceProvider>(),
            Substitute.For<IContentEntrySourceProviderCatalog>(),
            resolver);
        var fields = new Dictionary<string, JsonElement>
        {
            ["reference"] = JsonSerializer.SerializeToElement(new ContentEntryKey("view:sample", "entry-42"))
        };
        var body = new CreateContentItemRequest("Title", "entry", fields, null, null, "en-US");
        using var request = new HttpRequestMessage(
            operation == "create" ? HttpMethod.Post : HttpMethod.Put,
            operation == "create"
                ? "/api/v1/admin/content-items/article"
                : $"/api/v1/admin/content-items/article/{itemId}")
        {
            Content = JsonContent.Create(body)
        }.WithTestUser(12);

        using var response = await app.GetTestClient().SendAsync(request);

        response.StatusCode.ShouldBe(operation == "create" ? HttpStatusCode.Created : HttpStatusCode.OK);
        await resolver.Received(1).ResolveAsync(Scope.SiteId, Arg.Any<CancellationToken>());
        await actor.Received(1).SaveDraftAsync(
            Arg.Is<ContentItemViewModel>(item =>
                item.SiteId == Scope.SiteId
                && item.FieldsJson.Contains("view:sample", StringComparison.Ordinal)
                && item.FieldsJson.Contains("entry-42", StringComparison.Ordinal)),
            Scope.SiteId,
            Arg.Any<CancellationToken>());
    }

    private static ISelectedSiteScopeResolver CreateResolver()
    {
        var resolver = Substitute.For<ISelectedSiteScopeResolver>();
        resolver.ResolveAsync(Scope.SiteId, Arg.Any<CancellationToken>())
            .Returns(new SelectedSiteScope(Scope.TenantId, Scope.SiteId));
        return resolver;
    }

    private static AeroRequestResponse<ContentItemViewModel> SuccessfulItem(long id) => new(
        new ContentItemViewModel
        {
            Id = id,
            SiteId = Scope.SiteId,
            ContentTypeAlias = "article",
            Title = "Title",
            Slug = "entry",
            Culture = "en-US",
            FieldsJson = "{}"
        },
        new ContentItemErrorViewModel());

    private static async Task<WebApplication> CreateAppAsync(
        IAeroContentItemActor actor,
        IContentEntrySourceProvider provider,
        IContentEntrySourceProviderCatalog catalog,
        ISelectedSiteScopeResolver resolver)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddLogging();
        builder.Services.AddTestAuthentication();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton(actor);
        builder.Services.AddSingleton(Substitute.For<IContentQueryService>());
        builder.Services.AddSingleton(provider);
        builder.Services.AddSingleton(catalog);
        builder.Services.AddSingleton(resolver);
        builder.Services.AddSingleton<ISiteContext>(services =>
            new FeatureBackedSelectedSiteContext(
                services.GetRequiredService<IHttpContextAccessor>(),
                Scope.SiteId));

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapContentItemsApi();
        await app.StartAsync();
        return app;
    }

    private sealed class FeatureBackedSelectedSiteContext(
        IHttpContextAccessor accessor,
        long selectedSiteId) : ISiteContext
    {
        public long SiteId => accessor.HttpContext?.Features.Get<IAeroSiteSlice>()?.SiteId ?? selectedSiteId;
        public long TenantId => accessor.HttpContext?.Features.Get<IAeroSiteSlice>()?.TenantId ?? 0;
    }
}
