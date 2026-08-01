using System.Net;
using System.Net.Http.Json;
using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Abstractions.Requests;
using Aero.Cms.Abstractions.Validators;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Aliases.Areas.Api.v1;
using Aero.Cms.Modules.Sites;
using Aero.Core;
using Aero.Core.Http;
using AeroDB.Sable;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Aero.Cms.Core.Tests.Integration;

public sealed class AliasesSitePermissionAuthorizationTests
{
    private const long SiteId = 8111;
    private const long UserId = 8112;

    [Test]
    [Arguments("read", "GET")]
    [Arguments("create", "POST")]
    [Arguments("delete", "DELETE")]
    public async Task AssignedPermission_AllowsSelectedSite(string permission, string method)
    {
        await using var harness = await CreateHarnessAsync(permission);
        var actor = CreateActor();
        await using var app = await CreateAppAsync(harness.Session, actor);
        using var response = await app.GetTestClient().SendAsync(CreateRequest(method));
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    [Arguments("read", "GET")]
    [Arguments("create", "POST")]
    [Arguments("delete", "DELETE")]
    public async Task ForgedUnassignedSite_IsForbiddenBeforeActor(string permission, string method)
    {
        await using var harness = await CreateHarnessAsync(null);
        var actor = CreateActor();
        await using var app = await CreateAppAsync(harness.Session, actor);
        using var response = await app.GetTestClient().SendAsync(CreateRequest(method));
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await actor.DidNotReceiveWithAnyArgs().GetAllAliasesAsync(default, default);
        await actor.DidNotReceiveWithAnyArgs().CreateAliasAsync(default!, default, default);
        await actor.DidNotReceiveWithAnyArgs().GetByIdAsync(default, default, default);
        await actor.DidNotReceiveWithAnyArgs().DeleteAliasAsync(default, default, default);
    }

    private static IAeroAliasActor CreateActor()
    {
        var actor = Substitute.For<IAeroAliasActor>();
        actor.GetAllAliasesAsync(SiteId, Arg.Any<CancellationToken>()).Returns([]);
        actor.CreateAliasAsync(Arg.Any<CreateAliasRequest>(), SiteId, Arg.Any<CancellationToken>())
            .Returns(new AeroRequestResponse<AliasViewModel>(new AliasViewModel { Id = 1, SiteId = SiteId }, new AliasErrorViewModel()));
        actor.GetByIdAsync(1, SiteId, Arg.Any<CancellationToken>())
            .Returns(new AeroRequestResponse<AliasViewModel>(new AliasViewModel { Id = 1, SiteId = SiteId }, new AliasErrorViewModel()));
        actor.DeleteAliasAsync(1, SiteId, Arg.Any<CancellationToken>())
            .Returns(new AeroRequestResponse<AliasViewModel>(new AliasViewModel { Id = 1, SiteId = SiteId }, new AliasErrorViewModel()));
        return actor;
    }

    private static HttpRequestMessage CreateRequest(string method)
    {
        var request = method switch
        {
            "POST" => new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/aliases/")
            { Content = JsonContent.Create(new CreateAliasRequest(999, "/old", "/new")) },
            "DELETE" => new HttpRequestMessage(HttpMethod.Delete, "/api/v1/admin/aliases/1"),
            _ => new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/aliases/")
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
                Id = Snowflake.NewId(), UserId = UserId, SiteId = SiteId, Permissions = [permission]
            });
            await harness.Session.SaveChangesAsync();
        }
        return harness;
    }

    private static async Task<WebApplication> CreateAppAsync(IQuerySession query, IAeroAliasActor actor)
    {
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer(); builder.Services.AddLogging();
        builder.Services.AddTestAuthentication(); builder.Services.AddHttpContextAccessor(); builder.Services.AddSingleton(query);
        builder.Services.AddScoped<IAuthorizationHandler, SitePermissionHandler>();
        builder.Services.AddAuthorization(options =>
        {
            foreach (var permission in new[] { "read", "create", "delete" })
                options.AddPolicy($"site:{permission}", policy => policy.AddRequirements(new SitePermissionRequirement(permission)));
        });
        builder.Services.AddSingleton(actor);
        builder.Services.AddSingleton<IValidator<CreateAliasRequest>>(new CreateAliasRequestValidator());
        builder.Services.AddSingleton<IValidator<DeleteAliasRequest>>(new DeleteAliasRequestValidator());
        var site = Substitute.For<ISiteContext>(); site.SiteId.Returns(SiteId); builder.Services.AddSingleton(site);
        var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization(); app.MapAliasesApi(); await app.StartAsync(); return app;
    }
}
