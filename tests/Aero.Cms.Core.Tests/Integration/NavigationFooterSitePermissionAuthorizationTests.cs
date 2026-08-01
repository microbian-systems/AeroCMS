using System.Net;
using System.Net.Http.Json;
using Aero.Cms.Abstractions.Ai;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Footer.Areas.Api.v1;
using Aero.Cms.Modules.Footer.Domain;
using Aero.Cms.Modules.Footer.Services;
using Aero.Cms.Modules.Navigation.Areas.Api.v1;
using Aero.Cms.Modules.Navigation.Domain;
using Aero.Cms.Modules.Navigation.Services;
using Aero.Cms.Modules.Sites;
using Aero.Core;
using Aero.Core.Http;
using Aero.Core.Railway;
using AeroDB.Sable;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Aero.Cms.Core.Tests.Integration;

public sealed class NavigationFooterSitePermissionAuthorizationTests
{
    private const long SiteId = 8411;
    private const long UserId = 8412;

    [Test]
    [Arguments("navigation", "read", "read")]
    [Arguments("navigation", "create", "create")]
    [Arguments("navigation", "update", "update")]
    [Arguments("navigation", "delete", "delete")]
    [Arguments("navigation", "ai", "create,update")]
    [Arguments("footer", "read", "read")]
    [Arguments("footer", "create", "create")]
    [Arguments("footer", "update", "update")]
    [Arguments("footer", "delete", "delete")]
    [Arguments("footer", "ai", "create,update")]
    public async Task Assigned_permissions_allow_selected_site_and_invoke_service(
        string module,
        string action,
        string permissions)
    {
        await using var harness = await CreateHarnessAsync(permissions);
        var navigation = CreateNavigationService();
        var footer = CreateFooterService();
        await using var app = await CreateAppAsync(harness.Session, navigation, footer);

        using var response = await app.GetTestClient().SendAsync(CreateRequest(module, action));

        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Forbidden);
        await AssertInvoked(module, action, navigation, footer);
    }

    [Test]
    [Arguments("navigation", "read")]
    [Arguments("navigation", "create")]
    [Arguments("navigation", "update")]
    [Arguments("navigation", "delete")]
    [Arguments("navigation", "ai")]
    [Arguments("footer", "read")]
    [Arguments("footer", "create")]
    [Arguments("footer", "update")]
    [Arguments("footer", "delete")]
    [Arguments("footer", "ai")]
    public async Task Forged_unassigned_site_is_forbidden_before_service(
        string module,
        string action)
    {
        await using var harness = await CreateHarnessAsync(null);
        var navigation = CreateNavigationService();
        var footer = CreateFooterService();
        await using var app = await CreateAppAsync(harness.Session, navigation, footer);

        using var response = await app.GetTestClient().SendAsync(CreateRequest(module, action));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await AssertNotInvoked(navigation, footer);
    }

    [Test]
    [Arguments("navigation")]
    [Arguments("footer")]
    public async Task Ai_translate_requires_both_create_and_update_permissions(string module)
    {
        await using var harness = await CreateHarnessAsync("create");
        var navigation = CreateNavigationService();
        var footer = CreateFooterService();
        await using var app = await CreateAppAsync(harness.Session, navigation, footer);

        using var response = await app.GetTestClient().SendAsync(CreateRequest(module, "ai"));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await AssertNotInvoked(navigation, footer);
    }

    private static INavMenuService CreateNavigationService()
    {
        var service = Substitute.For<INavMenuService>();
        service.GetDetailAsync(1, Arg.Any<CancellationToken>())
            .Returns(Fail<NavigationDetail>());
        service.GetAsync(1, Arg.Any<CancellationToken>())
            .Returns(Fail<NavMenuDocument>());
        service.ForkToCultureAsync(1, "fr-FR", null, Arg.Any<CancellationToken>())
            .Returns(Fail<NavMenuDocument>());
        service.SetDefaultAsync(1, null, Arg.Any<CancellationToken>())
            .Returns(Fail<bool>());
        service.ArchiveAsync(1, 1, null, Arg.Any<CancellationToken>())
            .Returns(Fail<bool>());
        return service;
    }

    private static IFooterService CreateFooterService()
    {
        var service = Substitute.For<IFooterService>();
        service.GetDetailAsync(1, Arg.Any<CancellationToken>())
            .Returns(Fail<FooterDetail>());
        service.GetAsync(1, Arg.Any<CancellationToken>())
            .Returns(Fail<FooterDocument>());
        service.ForkToCultureAsync(1, "fr-FR", null, Arg.Any<CancellationToken>())
            .Returns(Fail<FooterDocument>());
        service.SetDefaultAsync(1, null, Arg.Any<CancellationToken>())
            .Returns(Fail<bool>());
        service.ArchiveAsync(1, 1, null, Arg.Any<CancellationToken>())
            .Returns(Fail<bool>());
        return service;
    }

    private static HttpRequestMessage CreateRequest(string module, string action)
    {
        var prefix = $"/api/v1/admin/{(module == "navigation" ? "navigations" : "footers")}";
        var request = action switch
        {
            "read" => new HttpRequestMessage(HttpMethod.Get, $"{prefix}/1"),
            "create" => new HttpRequestMessage(HttpMethod.Post, $"{prefix}/1/translations")
            {
                Content = module == "navigation"
                    ? JsonContent.Create(new ForkNavigationCultureRequest("fr-FR"))
                    : JsonContent.Create(new ForkFooterCultureRequest("fr-FR"))
            },
            "update" => new HttpRequestMessage(HttpMethod.Put, $"{prefix}/1/default"),
            "delete" => new HttpRequestMessage(HttpMethod.Delete, $"{prefix}/1?expectedVersion=1"),
            "ai" => new HttpRequestMessage(HttpMethod.Post, $"{prefix}/1/ai-translate")
            {
                Content = module == "navigation"
                    ? JsonContent.Create(new AiTranslateNavigationRequest(
                        [new AiTranslateNavigationCultureRequest("fr-FR")]))
                    : JsonContent.Create(new AiTranslateFooterRequest(
                        [new AiTranslateFooterCultureRequest("fr-FR")]))
            },
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };
        request.WithTestUser(UserId);
        request.Headers.Add("Cookie", $"AeroCms.SiteId={SiteId}");
        return request;
    }

    private static async Task AssertInvoked(
        string module,
        string action,
        INavMenuService navigation,
        IFooterService footer)
    {
        if (module == "navigation")
        {
            switch (action)
            {
                case "read":
                    await navigation.Received(1).GetDetailAsync(1, Arg.Any<CancellationToken>());
                    break;
                case "create":
                    await navigation.Received(1).ForkToCultureAsync(1, "fr-FR", null, Arg.Any<CancellationToken>());
                    break;
                case "update":
                    await navigation.Received(1).SetDefaultAsync(1, null, Arg.Any<CancellationToken>());
                    break;
                case "delete":
                    await navigation.Received(1).ArchiveAsync(1, 1, null, Arg.Any<CancellationToken>());
                    break;
                case "ai":
                    await navigation.Received(1).GetAsync(1, Arg.Any<CancellationToken>());
                    break;
            }
        }
        else
        {
            switch (action)
            {
                case "read":
                    await footer.Received(1).GetDetailAsync(1, Arg.Any<CancellationToken>());
                    break;
                case "create":
                    await footer.Received(1).ForkToCultureAsync(1, "fr-FR", null, Arg.Any<CancellationToken>());
                    break;
                case "update":
                    await footer.Received(1).SetDefaultAsync(1, null, Arg.Any<CancellationToken>());
                    break;
                case "delete":
                    await footer.Received(1).ArchiveAsync(1, 1, null, Arg.Any<CancellationToken>());
                    break;
                case "ai":
                    await footer.Received(1).GetAsync(1, Arg.Any<CancellationToken>());
                    break;
            }
        }
    }

    private static async Task AssertNotInvoked(
        INavMenuService navigation,
        IFooterService footer)
    {
        await navigation.DidNotReceiveWithAnyArgs().GetDetailAsync(default, default);
        await navigation.DidNotReceiveWithAnyArgs().GetAsync(default, default);
        await navigation.DidNotReceiveWithAnyArgs().ForkToCultureAsync(default, default!, default, default);
        await navigation.DidNotReceiveWithAnyArgs().SetDefaultAsync(default, default, default);
        await navigation.DidNotReceiveWithAnyArgs().ArchiveAsync(default, default, default, default);
        await footer.DidNotReceiveWithAnyArgs().GetDetailAsync(default, default);
        await footer.DidNotReceiveWithAnyArgs().GetAsync(default, default);
        await footer.DidNotReceiveWithAnyArgs().ForkToCultureAsync(default, default!, default, default);
        await footer.DidNotReceiveWithAnyArgs().SetDefaultAsync(default, default, default);
        await footer.DidNotReceiveWithAnyArgs().ArchiveAsync(default, default, default, default);
    }

    private static async Task<SableTestHarness> CreateHarnessAsync(string? permissions)
    {
        var harness = new SableTestHarness().WithSchema<UserSiteAssignment>(SchemaMode.Flexible);
        await harness.InitializeAsync();
        if (permissions is not null)
        {
            harness.Session.Store(new UserSiteAssignment
            {
                Id = Snowflake.NewId(),
                UserId = UserId,
                SiteId = SiteId,
                Permissions = permissions.Split(',').ToList()
            });
            await harness.Session.SaveChangesAsync();
        }
        return harness;
    }

    private static async Task<WebApplication> CreateAppAsync(
        IQuerySession query,
        INavMenuService navigation,
        IFooterService footer)
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
        builder.Services.AddSingleton(navigation);
        builder.Services.AddSingleton(footer);
        builder.Services.AddSingleton(Substitute.For<IAiContentTranslationService>());
        var site = Substitute.For<ISiteContext>();
        site.SiteId.Returns(SiteId);
        builder.Services.AddSingleton(site);

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapNavigationAdminApi();
        app.MapFooterAdminApi();
        await app.StartAsync();
        return app;
    }

    private static Task<Result<T, AeroError>> Fail<T>() =>
        Task.FromResult<Result<T, AeroError>>(
            AeroError.NotFoundError("Not found."));
}
