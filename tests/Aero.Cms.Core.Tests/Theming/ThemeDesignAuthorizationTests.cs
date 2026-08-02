using System.Net;
using Aero.Cms.Core.Entities;
using Aero.Cms.Core.Tests.Integration;
using Aero.Cms.Modules.Theming;
using AeroDB.Sable;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Core.Tests.Theming;

public sealed class ThemeDesignAuthorizationTests
{
    private const long SelectedSiteId = 4101;
    private const long OtherSiteId = 4102;
    private const long UserId = 4201;

    [Test]
    public async Task Exact_design_permission_for_selected_site_is_allowed()
    {
        await using var harness = await CreateHarnessAsync(SelectedSiteId, ["design"]);
        await using var app = await CreateAppAsync(harness.Session);

        using var response = await app.GetTestClient().SendAsync(CreateRequest());

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Ordinary_update_permission_does_not_grant_theme_design_trust()
    {
        await using var harness = await CreateHarnessAsync(SelectedSiteId, ["update"]);
        await using var app = await CreateAppAsync(harness.Session);

        using var response = await app.GetTestClient().SendAsync(CreateRequest());

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Design_permission_for_another_site_is_forbidden()
    {
        await using var harness = await CreateHarnessAsync(OtherSiteId, ["design"]);
        await using var app = await CreateAppAsync(harness.Session);

        using var response = await app.GetTestClient().SendAsync(CreateRequest());

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Administrator_can_design_the_selected_site_without_an_assignment()
    {
        await using var harness = await CreateHarnessAsync(null, []);
        await using var app = await CreateAppAsync(harness.Session);

        using var request = CreateRequest(isAdmin: true);
        using var response = await app.GetTestClient().SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    private static HttpRequestMessage CreateRequest(bool isAdmin = false)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/theme-design")
            .WithTestUser(UserId, isAdmin: isAdmin);
        request.Headers.Add("Cookie", $"AeroCms.SiteId={SelectedSiteId}");
        return request;
    }

    private static async Task<SableTestHarness> CreateHarnessAsync(
        long? assignedSiteId,
        IReadOnlyList<string> permissions)
    {
        var harness = new SableTestHarness()
            .WithSchema<UserSiteAssignment>(SchemaMode.Flexible);
        await harness.InitializeAsync();

        if (assignedSiteId.HasValue)
        {
            harness.Session.Store(new UserSiteAssignment
            {
                Id = 4301,
                UserId = UserId,
                SiteId = assignedSiteId.Value,
                Permissions = permissions.ToList()
            });
            await harness.Session.SaveChangesAsync();
        }

        return harness;
    }

    private static async Task<WebApplication> CreateAppAsync(IQuerySession querySession)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddLogging();
        builder.Services.AddTestAuthentication();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton(querySession);
        builder.Services.AddScoped<IAuthorizationHandler, ThemeDesignPermissionHandler>();
        builder.Services.AddAuthorization(options =>
            options.AddPolicy("theme:design", policy =>
                policy.AddRequirements(new ThemeDesignPermissionRequirement())));

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGet("/theme-design", () => Results.Ok())
            .RequireAuthorization("theme:design");
        await app.StartAsync();
        return app;
    }
}
