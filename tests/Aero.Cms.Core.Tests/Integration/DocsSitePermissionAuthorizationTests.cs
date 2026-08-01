using System.Net;
using System.Net.Http.Json;
using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Docs.Areas.Api.v1;
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

public sealed class DocsSitePermissionAuthorizationTests
{
    private const long SiteId = 941;
    private const long UserId = 942;
    private const long DocId = 943;

    [Test]
    [Arguments("read", "GET", "/api/v1/admin/docs/943")]
    [Arguments("create", "POST", "/api/v1/admin/docs/")]
    [Arguments("update", "POST", "/api/v1/admin/docs/943/publish")]
    [Arguments("delete", "DELETE", "/api/v1/admin/docs/943")]
    public async Task AssignedSelectedSiteCookie_AllowsEachDocsPermission(string permission, string method, string path)
    {
        await using var harness = await CreateHarnessAsync(permission, assigned: true);
        var actor = CreateActor();
        await using var app = await CreateAppAsync(harness.Session, actor, permission);
        using var request = CreateRequest(method, path);

        using var response = await app.GetTestClient().SendAsync(request);

        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    [Arguments("read", "GET", "/api/v1/admin/docs/943")]
    [Arguments("create", "POST", "/api/v1/admin/docs/")]
    [Arguments("update", "POST", "/api/v1/admin/docs/943/publish")]
    [Arguments("delete", "DELETE", "/api/v1/admin/docs/943")]
    public async Task ForgedUnassignedSelectedSiteCookie_ReturnsForbiddenBeforeDocsActor(string permission, string method, string path)
    {
        await using var harness = await CreateHarnessAsync(permission, assigned: false);
        var actor = CreateActor();
        await using var app = await CreateAppAsync(harness.Session, actor, permission);
        using var request = CreateRequest(method, path);

        using var response = await app.GetTestClient().SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await actor.DidNotReceive().GetByIdAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CancellationToken>());
        await actor.DidNotReceive().SaveAsync(Arg.Any<DocViewModel>(), Arg.Any<long>(), Arg.Any<CancellationToken>());
        await actor.DidNotReceive().PublishAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CancellationToken>());
        await actor.DidNotReceive().DeleteDocAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    private static IAeroDocsActor CreateActor()
    {
        var actor = Substitute.For<IAeroDocsActor>();
        var ok = new AeroRequestResponse<DocViewModel>(
            new DocViewModel { Id = DocId, SiteId = SiteId, Title = "Authorized", Slug = "authorized" },
            new DocErrorViewModel());
        actor.GetByIdAsync(DocId, SiteId, Arg.Any<CancellationToken>()).Returns(ok);
        actor.SaveAsync(Arg.Any<DocViewModel>(), SiteId, Arg.Any<CancellationToken>()).Returns(ok);
        actor.PublishAsync(DocId, SiteId, Arg.Any<CancellationToken>()).Returns(ok);
        actor.DeleteDocAsync(DocId, SiteId, Arg.Any<CancellationToken>()).Returns(ok);
        return actor;
    }

    private static HttpRequestMessage CreateRequest(string method, string path)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        request.WithTestUser(UserId);
        request.Headers.Add("Cookie", $"AeroCms.SiteId={SiteId}");
        if (method == "POST" && path.EndsWith("/docs/", StringComparison.Ordinal))
            request.Content = JsonContent.Create(new DocViewModel { Title = "New doc", Slug = "new-doc" });
        return request;
    }

    private static async Task<SableTestHarness> CreateHarnessAsync(string permission, bool assigned)
    {
        var harness = new SableTestHarness().WithSchema<UserSiteAssignment>(SchemaMode.Flexible);
        await harness.InitializeAsync();
        if (assigned)
        {
            harness.Session.Store(new UserSiteAssignment { Id = Snowflake.NewId(), UserId = UserId, SiteId = SiteId, Permissions = [permission] });
            await harness.Session.SaveChangesAsync();
        }
        return harness;
    }

    private static async Task<WebApplication> CreateAppAsync(IQuerySession querySession, IAeroDocsActor actor, string permission)
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
            foreach (var value in new[] { "read", "create", "update", "delete" })
                options.AddPolicy($"site:{value}", policy => policy.AddRequirements(new SitePermissionRequirement(value)));
        });
        builder.Services.AddSingleton(actor);
        var site = Substitute.For<ISiteContext>(); site.SiteId.Returns(SiteId); builder.Services.AddSingleton(site);
        var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization(); app.MapDocsApi(); await app.StartAsync(); return app;
    }
}
