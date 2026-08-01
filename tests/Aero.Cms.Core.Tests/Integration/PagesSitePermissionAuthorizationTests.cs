using System.Net;
using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Pages.Areas.Api.v1;
using Aero.Cms.Modules.Sites;
using Aero.Core;
using Aero.Core.Http;
using AeroDB.Sable;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Aero.Cms.Core.Tests.Integration;

public sealed class PagesSitePermissionAuthorizationTests
{
    private const long SiteId = 811;
    private const long UserId = 812;
    private const long PageId = 813;

    [Test]
    public async Task AssignedSelectedSiteCookie_AllowsReadAndInvokesScopedActor()
    {
        await using var harness = await CreateHarnessAsync(withAssignment: true);
        var actor = CreateActor();
        await using var app = await CreateAppAsync(harness.Session, actor);
        using var request = CreateRequest();

        using var response = await app.GetTestClient().SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await actor.Received(1).GetByIdAsync(PageId, SiteId, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ForgedUnassignedSelectedSiteCookie_ReturnsForbiddenBeforeActor()
    {
        await using var harness = await CreateHarnessAsync(withAssignment: false);
        var actor = CreateActor();
        await using var app = await CreateAppAsync(harness.Session, actor);
        using var request = CreateRequest();

        using var response = await app.GetTestClient().SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await actor.DidNotReceive().GetByIdAsync(
            Arg.Any<long>(),
            Arg.Any<long>(),
            Arg.Any<CancellationToken>());
    }

    private static IAeroPageActor CreateActor()
    {
        var actor = Substitute.For<IAeroPageActor>();
        actor.GetByIdAsync(PageId, SiteId, Arg.Any<CancellationToken>())
            .Returns(new AeroRequestResponse<PageViewModel>(
                new PageViewModel
                {
                    Id = PageId,
                    SiteId = SiteId,
                    Title = "Authorized page",
                    Slug = "authorized-page"
                },
                new PageErrorViewModel()));
        return actor;
    }

    private static HttpRequestMessage CreateRequest()
    {
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/{HttpConstants.ApiPrefix}admin/pages/{PageId}");
        request.WithTestUser(UserId);
        request.Headers.Add("Cookie", $"AeroCms.SiteId={SiteId}");
        return request;
    }

    private static async Task<SableTestHarness> CreateHarnessAsync(bool withAssignment)
    {
        var harness = new SableTestHarness()
            .WithSchema<UserSiteAssignment>(SchemaMode.Flexible);
        await harness.InitializeAsync();

        if (withAssignment)
        {
            harness.Session.Store(new UserSiteAssignment
            {
                Id = Snowflake.NewId(),
                UserId = UserId,
                SiteId = SiteId,
                Permissions = ["read"]
            });
            await harness.Session.SaveChangesAsync();
        }

        return harness;
    }

    private static async Task<WebApplication> CreateAppAsync(
        IQuerySession querySession,
        IAeroPageActor actor)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddLogging();
        builder.Services.AddTestAuthentication();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton(querySession);
        builder.Services.AddScoped<IAuthorizationHandler, SitePermissionHandler>();
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(
                "site:read",
                policy => policy.AddRequirements(new SitePermissionRequirement("read")));
        });
        builder.Services.AddSingleton(actor);
        var siteContext = Substitute.For<ISiteContext>();
        siteContext.SiteId.Returns(SiteId);
        builder.Services.AddSingleton(siteContext);

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapPagesApi();
        await app.StartAsync();
        return app;
    }
}
