using System.Net;
using System.Net.Http.Json;
using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Core.Content.Services;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Content.Areas.Api.v1;
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

public sealed class ContentSitePermissionAuthorizationTests
{
    private const long SiteId = 8211;
    private const long UserId = 8212;

    [Test]
    [Arguments("read", "GET")]
    [Arguments("create", "POST")]
    [Arguments("update", "PUT")]
    [Arguments("delete", "DELETE")]
    public async Task Assigned_permission_allows_selected_site_and_actor_receives_that_site(
        string permission,
        string method)
    {
        await using var harness = await CreateHarnessAsync(permission);
        var actor = CreateActor();
        await using var app = await CreateAppAsync(harness.Session, actor);

        using var response = await app.GetTestClient().SendAsync(CreateRequest(method));

        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Forbidden);
        switch (method)
        {
            case "GET":
                await actor.Received(1).GetByIdAsync(1, SiteId, Arg.Any<CancellationToken>());
                break;
            case "POST":
                await actor.Received(1).SaveDraftAsync(
                    Arg.Is<ContentItemViewModel>(item => item.SiteId == SiteId),
                    SiteId,
                    Arg.Any<CancellationToken>());
                break;
            case "PUT":
                await actor.Received(1).GetByIdAsync(1, SiteId, Arg.Any<CancellationToken>());
                await actor.Received(1).SaveDraftAsync(
                    Arg.Any<ContentItemViewModel>(),
                    SiteId,
                    Arg.Any<CancellationToken>());
                break;
            case "DELETE":
                await actor.Received(1).GetByIdAsync(1, SiteId, Arg.Any<CancellationToken>());
                await actor.Received(1).DeleteAsync(1, SiteId, Arg.Any<CancellationToken>());
                break;
        }
    }

    [Test]
    [Arguments("read", "GET")]
    [Arguments("create", "POST")]
    [Arguments("update", "PUT")]
    [Arguments("delete", "DELETE")]
    public async Task Forged_unassigned_site_is_forbidden_before_actor(
        string permission,
        string method)
    {
        await using var harness = await CreateHarnessAsync(null);
        var actor = CreateActor();
        await using var app = await CreateAppAsync(harness.Session, actor);

        using var response = await app.GetTestClient().SendAsync(CreateRequest(method));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await actor.DidNotReceiveWithAnyArgs().GetByIdAsync(default, default, default);
        await actor.DidNotReceiveWithAnyArgs().SaveDraftAsync(default!, default, default);
        await actor.DidNotReceiveWithAnyArgs().DeleteAsync(default, default, default);
    }

    private static IAeroContentItemActor CreateActor()
    {
        var actor = Substitute.For<IAeroContentItemActor>();
        var item = new ContentItemViewModel
        {
            Id = 1,
            SiteId = SiteId,
            ContentTypeAlias = "article",
            Title = "Title",
            Slug = "entry",
            Culture = "en-US",
            FieldsJson = "{}"
        };
        actor.GetByIdAsync(1, SiteId, Arg.Any<CancellationToken>())
            .Returns(new AeroRequestResponse<ContentItemViewModel>(item, new ContentItemErrorViewModel()));
        actor.SaveDraftAsync(Arg.Any<ContentItemViewModel>(), SiteId, Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var saved = call.Arg<ContentItemViewModel>();
                saved.Id = saved.Id == 0 ? 2 : saved.Id;
                return new AeroRequestResponse<ContentItemViewModel>(saved, new ContentItemErrorViewModel());
            });
        actor.DeleteAsync(1, SiteId, Arg.Any<CancellationToken>())
            .Returns(new AeroRequestResponse<ContentItemViewModel>(new(), new ContentItemErrorViewModel()));
        return actor;
    }

    private static HttpRequestMessage CreateRequest(string method)
    {
        var body = new CreateContentItemRequest(
            "Title",
            "entry",
            new Dictionary<string, System.Text.Json.JsonElement>(),
            null,
            null,
            "en-US");
        var request = method switch
        {
            "POST" => new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/content-items/article")
            {
                Content = JsonContent.Create(body)
            },
            "PUT" => new HttpRequestMessage(HttpMethod.Put, "/api/v1/admin/content-items/article/1")
            {
                Content = JsonContent.Create(body)
            },
            "DELETE" => new HttpRequestMessage(HttpMethod.Delete, "/api/v1/admin/content-items/article/1"),
            _ => new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/content-items/article/1")
        };
        request.WithTestUser(UserId);
        request.Headers.Add("Cookie", $"AeroCms.SiteId={SiteId}");
        return request;
    }

    private static async Task<SableTestHarness> CreateHarnessAsync(string? permission)
    {
        var harness = new SableTestHarness().WithSchema<UserSiteAssignment>(SchemaMode.Flexible);
        await harness.InitializeAsync();
        if (permission is not null)
        {
            harness.Session.Store(new UserSiteAssignment
            {
                Id = Snowflake.NewId(),
                UserId = UserId,
                SiteId = SiteId,
                Permissions = [permission]
            });
            await harness.Session.SaveChangesAsync();
        }
        return harness;
    }

    private static async Task<WebApplication> CreateAppAsync(
        IQuerySession query,
        IAeroContentItemActor actor)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddLogging();
        builder.Services.AddTestAuthentication();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton(query);
        builder.Services.AddScoped<IAuthorizationHandler, SitePermissionHandler>();
        builder.Services.AddAuthorization(options =>
        {
            foreach (var permission in new[] { "read", "create", "update", "delete" })
                options.AddPolicy(
                    $"site:{permission}",
                    policy => policy.AddRequirements(new SitePermissionRequirement(permission)));
        });
        builder.Services.AddSingleton(actor);
        builder.Services.AddSingleton(Substitute.For<IContentQueryService>());
        var site = Substitute.For<ISiteContext>();
        site.SiteId.Returns(SiteId);
        builder.Services.AddSingleton(site);

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapContentItemsApi();
        await app.StartAsync();
        return app;
    }
}
