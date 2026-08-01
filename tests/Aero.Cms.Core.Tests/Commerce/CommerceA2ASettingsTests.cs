using System.Net;
using System.Net.Http.Json;
using Aero.Cms.Core.Entities;
using Aero.Cms.Core.Tests.Integration;
using Aero.Cms.Modules.Commerce;
using Aero.Cms.Modules.Commerce.A2A.Api;
using Aero.Cms.Modules.Commerce.A2A.Models;
using Aero.Cms.Modules.Commerce.A2A.Services;
using Aero.Cms.Modules.Commerce.A2A.Validation;
using Aero.Cms.Modules.Commerce.Catalog.Services;
using Aero.Cms.Modules.Sites;
using Aero.Core;
using Aero.Core.Http;
using Aero.Core.Railway;
using AeroDB.Sable;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Aero.Cms.Core.Tests.Commerce;

public sealed class CommerceA2ASettingsTests
{
    private const long TenantId = 42;
    private const long SiteId = 10;
    private const long ForeignSiteId = 11;
    private const long UserId = 77;

    [Test]
    public async Task Settings_default_to_disabled_when_no_document_exists()
    {
        await using var harness = await CreateHarnessAsync();
        var service = CreateService(harness.Session);

        var result = await service.GetAsync(TenantId, SiteId);

        result.ShouldBeOfType<Result<A2ASettingsResponse, AeroError>.Ok>().Value.ShouldBe(new A2ASettingsResponse(false));
    }

    [Test]
    public async Task Settings_reads_and_writes_are_isolated_to_the_explicit_tenant_and_site_scope()
    {
        await using var harness = await CreateHarnessAsync();
        var service = CreateService(harness.Session);

        (await service.UpdateAsync(TenantId, SiteId, new UpdateA2ASettingsRequest(true), "admin"))
            .ShouldBeOfType<Result<A2ASettingsResponse, AeroError>.Ok>().Value.ShouldBe(new A2ASettingsResponse(true));
        (await service.UpdateAsync(TenantId, ForeignSiteId, new UpdateA2ASettingsRequest(false), "admin"))
            .ShouldBeOfType<Result<A2ASettingsResponse, AeroError>.Ok>().Value.ShouldBe(new A2ASettingsResponse(false));

        (await service.GetAsync(TenantId, SiteId))
            .ShouldBeOfType<Result<A2ASettingsResponse, AeroError>.Ok>().Value.ShouldBe(new A2ASettingsResponse(true));
        (await service.GetAsync(TenantId, ForeignSiteId))
            .ShouldBeOfType<Result<A2ASettingsResponse, AeroError>.Ok>().Value.ShouldBe(new A2ASettingsResponse(false));
        (await service.GetAsync(TenantId + 1, SiteId))
            .ShouldBeOfType<Result<A2ASettingsResponse, AeroError>.Ok>().Value.ShouldBe(new A2ASettingsResponse(false));

        var documents = await harness.Session.Query<A2ASettingsDocument>().ToListAsync();
        documents.Count.ShouldBe(2);
        documents.All(x => x.Id > 0).ShouldBeTrue();
        documents.Single(x => x.SiteId == SiteId).TenantId.ShouldBe(TenantId);
        documents.Single(x => x.SiteId == ForeignSiteId).TenantId.ShouldBe(TenantId);
    }

    [Test]
    public async Task Cms_administrator_can_enable_a2a_for_the_authorized_selected_site()
    {
        await using var harness = await CreateHttpHarnessAsync();
        await using var app = await CreateHttpAppAsync(harness.Session);
        using var request = ManagerRequest(HttpMethod.Put, SiteId, isAdmin: true);
        request.Content = JsonContent.Create(new { isEnabled = true });

        using var response = await app.GetTestClient().SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<A2ASettingsResponse>()).ShouldBe(new A2ASettingsResponse(true));
        var stored = await harness.Session.Query<A2ASettingsDocument>()
            .FirstOrDefaultAsync(x => x.TenantId == TenantId && x.SiteId == SiteId);
        stored.ShouldNotBeNull();
        stored!.IsEnabled.ShouldBeTrue();
        stored.Id.ShouldBeGreaterThan(0);
    }

    [Test]
    public async Task Non_administrator_and_foreign_selected_site_are_rejected_without_persisting_changes()
    {
        await using var harness = await CreateHttpHarnessAsync();
        await using var app = await CreateHttpAppAsync(harness.Session);

        using var delegatedRequest = ManagerRequest(HttpMethod.Put, SiteId, isAdmin: false);
        delegatedRequest.Content = JsonContent.Create(new { isEnabled = true });
        using var delegatedResponse = await app.GetTestClient().SendAsync(delegatedRequest);
        delegatedResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using var foreignRequest = ManagerRequest(HttpMethod.Put, ForeignSiteId, isAdmin: false);
        foreignRequest.Content = JsonContent.Create(new { isEnabled = true });
        using var foreignResponse = await app.GetTestClient().SendAsync(foreignRequest);
        foreignResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        (await harness.Session.Query<A2ASettingsDocument>().ToListAsync()).ShouldBeEmpty();
    }

    private static A2ASettingsService CreateService(IDocumentSession session)
        => new(new A2ASettingsRepository(session), new UpdateA2ASettingsRequestValidator());

    private static async Task<SableTestHarness> CreateHarnessAsync()
    {
        var harness = new SableTestHarness()
            .WithConfiguration(new CommerceModule().Configure)
            .WithSchema<SitesModel>(SchemaMode.Flexible)
            .WithSchema<UserSiteAssignment>(SchemaMode.Flexible);
        await harness.InitializeAsync();
        return harness;
    }

    private static async Task<SableTestHarness> CreateHttpHarnessAsync()
    {
        var harness = await CreateHarnessAsync();
        harness.Session.Store(new SitesModel { Id = SiteId, TenantId = TenantId, Name = "Store", IsEnabled = true });
        harness.Session.Store(new SitesModel { Id = ForeignSiteId, TenantId = TenantId, Name = "Foreign store", IsEnabled = true });
        harness.Session.Store(new UserSiteAssignment
        {
            Id = 900,
            UserId = UserId,
            SiteId = SiteId,
            Permissions = ["read", "update"]
        });
        await harness.Session.SaveChangesAsync();
        return harness;
    }

    private static async Task<WebApplication> CreateHttpAppAsync(IDocumentSession session)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddLogging();
        builder.Services.AddTestAuthentication();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<IQuerySession>(session);
        builder.Services.AddSingleton(session);
        builder.Services.AddScoped<IAuthorizationHandler, SitePermissionHandler>();
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("site:read", policy => policy.AddRequirements(new SitePermissionRequirement("read")));
            options.AddPolicy("site:update", policy => policy.AddRequirements(new SitePermissionRequirement("update")));
        });
        builder.Services.AddScoped<ISiteContext, CookieSiteContext>();
        builder.Services.AddScoped<ICommerceManagerScopeResolver, CommerceManagerScopeResolver>();
        builder.Services.AddScoped<IA2ASettingsRepository, A2ASettingsRepository>();
        builder.Services.AddScoped<IA2ASettingsService, A2ASettingsService>();
        builder.Services.AddScoped<IValidator<UpdateA2ASettingsRequest>, UpdateA2ASettingsRequestValidator>();

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapA2ASettingsApi();
        await app.StartAsync();
        return app;
    }

    private static HttpRequestMessage ManagerRequest(HttpMethod method, long selectedSiteId, bool isAdmin)
    {
        var request = new HttpRequestMessage(method, "/api/v1/admin/commerce/a2a/settings")
            .WithTestUser(UserId, isAdmin: isAdmin);
        request.Headers.Add("Cookie", $"AeroCms.SiteId={selectedSiteId}");
        return request;
    }

    private sealed class CookieSiteContext(IHttpContextAccessor accessor) : ISiteContext
    {
        public long SiteId => long.TryParse(accessor.HttpContext?.Request.Cookies["AeroCms.SiteId"], out var siteId) ? siteId : 0;
        public long TenantId => 999;
    }
}
