using System.Net;
using System.Net.Http.Json;
using Aero.Cms.Abstractions.Content.Views;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Modules.Content.Areas.Api.v1;
using Aero.Cms.Core.Infrastructure;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Core.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;

namespace Aero.Cms.Core.Tests.Integration;

public sealed class VirtualContentEntriesApiTests
{
    private static readonly ContentViewScope Scope = new(41, 84);

    [Test]
    public async Task Admin_route_derives_tenant_from_persisted_selected_site()
    {
        var provider = Substitute.For<IContentEntrySourceProvider>();
        provider.Provider.Returns("registry");
        provider.FindAsync(Scope, "entry-42", Arg.Any<CancellationToken>())
            .Returns(new ContentEntry(
                new ContentEntryKey("registry", "entry-42"),
                Scope,
                new Dictionary<string, object?> { ["title"] = "Sample entry" }));
        var scopeResolver = Substitute.For<ISelectedSiteScopeResolver>();
        scopeResolver.ResolveAsync(Scope.SiteId, Arg.Any<CancellationToken>())
            .Returns(new SelectedSiteScope(Scope.TenantId, Scope.SiteId));
        await using var app = await CreateAppAsync(
            provider,
            scopeResolver: scopeResolver,
            resolveSelectedSite: true);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/v1/admin/content-views/entries/registry/entry-42").WithTestUser(12);

        using var response = await app.GetTestClient().SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await scopeResolver.Received(1).ResolveAsync(Scope.SiteId, Arg.Any<CancellationToken>());
        await provider.Received(1).FindAsync(Scope, "entry-42", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Missing_provider_entry_returns_404_and_uses_server_scope()
    {
        var provider = Substitute.For<IContentEntrySourceProvider>();
        provider.Provider.Returns("registry");
        provider.FindAsync(Scope, "missing", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ContentEntry?>(null));
        await using var app = await CreateAppAsync(provider);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/v1/admin/content-views/entries/registry/missing").WithTestUser(12);

        using var response = await app.GetTestClient().SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await provider.Received(1).FindAsync(Scope, "missing", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Entry_get_rejects_provider_data_from_another_site()
    {
        var provider = Substitute.For<IContentEntrySourceProvider>();
        provider.Provider.Returns("registry");
        provider.FindAsync(Scope, "entry-42", Arg.Any<CancellationToken>())
            .Returns(new ContentEntry(
                new ContentEntryKey("registry", "entry-42"),
                new ContentViewScope(Scope.TenantId, Scope.SiteId + 1),
                new Dictionary<string, object?> { ["title"] = "Sample entry" }));
        await using var app = await CreateAppAsync(provider);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/v1/admin/content-views/entries/registry/entry-42").WithTestUser(12);

        using var response = await app.GetTestClient().SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Search_is_bounded_and_filters_wrong_scope_rows()
    {
        var provider = Substitute.For<IContentEntrySourceProvider>();
        provider.Provider.Returns("registry");
        provider.SearchAsync(Scope, null, "sample", 100, Arg.Any<CancellationToken>())
            .Returns(new ContentEntry[]
            {
                new(
                    new ContentEntryKey("registry", "entry-42"),
                    Scope,
                    new Dictionary<string, object?> { ["title"] = "Sample entry" }),
                new(
                    new ContentEntryKey("registry", "wrong-site"),
                    new ContentViewScope(Scope.TenantId, Scope.SiteId + 1),
                    new Dictionary<string, object?> { ["title"] = "Other" })
            });
        await using var app = await CreateAppAsync(provider);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/v1/admin/content-views/entries/registry?query=sample&take=999").WithTestUser(12);

        using var response = await app.GetTestClient().SendAsync(request);
        var entries = await response.Content.ReadFromJsonAsync<IReadOnlyList<VirtualContentEntryOption>>();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        entries.ShouldNotBeNull();
        entries!.Count.ShouldBe(1);
        entries[0].StableId.ShouldBe("entry-42");
        entries[0].Title.ShouldBe("Sample entry");
    }

    [Test]
    public async Task Published_view_provider_is_discovered_and_resolved_through_catalog()
    {
        var staticProvider = Substitute.For<IContentEntrySourceProvider>();
        staticProvider.Provider.Returns("registry");
        var viewProvider = Substitute.For<IContentEntrySourceProvider>();
        viewProvider.Provider.Returns("view:catalog");
        viewProvider.FindAsync(Scope, "entry-42", Arg.Any<CancellationToken>())
            .Returns(new ContentEntry(
                new ContentEntryKey("view:catalog", "entry-42"),
                Scope,
                new Dictionary<string, object?> { ["title"] = "Sample entry" }));
        var catalog = Substitute.For<IContentEntrySourceProviderCatalog>();
        catalog.ListProviderKeysAsync(Scope, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<string>>(["view:catalog"]));
        catalog.ResolveAsync(Scope, "view:catalog", Arg.Any<CancellationToken>())
            .Returns(viewProvider);
        await using var app = await CreateAppAsync(staticProvider, catalog);

        using var providersRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/content-views/entries").WithTestUser(12);
        using var providersResponse = await app.GetTestClient().SendAsync(providersRequest);
        var providerOptions = await providersResponse.Content.ReadFromJsonAsync<IReadOnlyList<ContentEntryProviderOption>>();
        using var entryRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/content-views/entries/view%3Acatalog/entry-42").WithTestUser(12);
        using var entryResponse = await app.GetTestClient().SendAsync(entryRequest);

        providersResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        providerOptions.ShouldNotBeNull();
        providerOptions!.Select(option => option.Provider).ShouldContain("view:catalog");
        entryResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        await viewProvider.Received(1).FindAsync(Scope, "entry-42", Arg.Any<CancellationToken>());
    }

    private static async Task<WebApplication> CreateAppAsync(
        IContentEntrySourceProvider provider,
        IContentEntrySourceProviderCatalog? providerCatalog = null,
        ISiteContext? siteContext = null,
        ISelectedSiteScopeResolver? scopeResolver = null,
        bool resolveSelectedSite = false)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddLogging();
        builder.Services.AddTestAuthentication();
        builder.Services.AddSingleton(provider);
        if (providerCatalog is null)
        {
            providerCatalog = Substitute.For<IContentEntrySourceProviderCatalog>();
            providerCatalog.ListProviderKeysAsync(Scope, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IReadOnlyList<string>>([]));
            providerCatalog.ResolveAsync(Scope, Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IContentEntrySourceProvider?>(null));
        }
        builder.Services.AddSingleton(providerCatalog);
        if (resolveSelectedSite)
        {
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddSingleton<ISiteContext>(services =>
                new FeatureBackedSelectedSiteContext(
                    services.GetRequiredService<IHttpContextAccessor>(),
                    Scope.SiteId));
        }
        else if (siteContext is null)
        {
            siteContext = Substitute.For<ISiteContext>();
            siteContext.TenantId.Returns(Scope.TenantId);
            siteContext.SiteId.Returns(Scope.SiteId);
            builder.Services.AddSingleton(siteContext);
        }
        else
        {
            builder.Services.AddSingleton(siteContext);
        }
        if (scopeResolver is not null)
            builder.Services.AddSingleton(scopeResolver);

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapContentViewsApi();
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
