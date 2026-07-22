using System.Net;
using System.Net.Http.Json;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Abstractions.Requests;
using Aero.Cms.Modules.Sites;
using Aero.Cms.Modules.Sites.Events;
using Aero.Core;
using Aero.Core.Http;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Wolverine;

namespace Aero.Cms.Core.Tests.Integration;

public sealed class SitesApiAuthorizationTests
{
    private const long SiteId = 481;
    private const long UserId = 72;

    [Test]
    public async Task AssignedUserCanSelectSiteAndPublishesAuditEvent()
    {
        var dependencies = CreateDependencies(hasReadPermission: true);
        await using var app = await CreateAppAsync(dependencies);
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/{HttpConstants.ApiPrefix}admin/sites/current")
        {
            Content = JsonContent.Create(new SetCurrentSiteRequest(SiteId))
        };
        request.WithTestUser(UserId);

        using var response = await client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(response.Headers.TryGetValues("Set-Cookie", out var cookieValues)).IsTrue();
        await Assert.That(cookieValues!.Any(value =>
            value.Contains($"AeroCms.SiteId={SiteId}", StringComparison.Ordinal))).IsTrue();
        await dependencies.UserSiteService.Received(1)
            .HasPermissionAsync(UserId, SiteId, "read", Arg.Any<CancellationToken>());
        await dependencies.Bus.Received(1).PublishAsync(
            Arg.Is<SiteSelectionChanged>(message =>
                message.SiteId == SiteId && message.UserId == UserId));
    }

    [Test]
    public async Task UnassignedUserCannotSelectSiteOrPublishAuditEvent()
    {
        var dependencies = CreateDependencies(hasReadPermission: false);
        await using var app = await CreateAppAsync(dependencies);
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/{HttpConstants.ApiPrefix}admin/sites/current")
        {
            Content = JsonContent.Create(new SetCurrentSiteRequest(SiteId))
        };
        request.WithTestUser(UserId);

        using var response = await client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(response.Headers.Contains("Set-Cookie")).IsFalse();
        await dependencies.SiteLookup.DidNotReceive()
            .GetAllAsync(Arg.Any<CancellationToken>());
        await dependencies.Bus.DidNotReceive()
            .PublishAsync(Arg.Any<SiteSelectionChanged>());
    }

    [Test]
    public async Task ForgedCurrentSiteCookieReturnsForbiddenWithoutPayload()
    {
        var dependencies = CreateDependencies(hasReadPermission: false);
        await using var app = await CreateAppAsync(dependencies);
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/{HttpConstants.ApiPrefix}admin/sites/current");
        request.WithTestUser(UserId);
        request.Headers.Add("Cookie", $"AeroCms.SiteId={SiteId}");

        using var response = await client.SendAsync(request);
        var payload = await response.Content.ReadAsStringAsync();

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(payload).IsEmpty();
        await dependencies.SiteLookup.DidNotReceive()
            .GetAllAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AdminRoleCanSelectSiteWithoutAssignment()
    {
        var dependencies = CreateDependencies(hasReadPermission: false);
        await using var app = await CreateAppAsync(dependencies);
        using var client = app.GetTestClient();
        using var request = CreateSelectionRequest();
        request.WithTestUser(UserId, role: "Admin");

        using var response = await client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await dependencies.UserSiteService.DidNotReceive()
            .HasPermissionAsync(
                Arg.Any<long>(),
                Arg.Any<long>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExactIsAdminClaimCanSelectSiteWithoutAssignment()
    {
        var dependencies = CreateDependencies(hasReadPermission: false);
        await using var app = await CreateAppAsync(dependencies);
        using var client = app.GetTestClient();
        using var request = CreateSelectionRequest();
        request.WithTestUser(UserId, isAdmin: true);

        using var response = await client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await dependencies.UserSiteService.DidNotReceive()
            .HasPermissionAsync(
                Arg.Any<long>(),
                Arg.Any<long>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AnonymousSiteSelectionReturnsUnauthorized()
    {
        var dependencies = CreateDependencies(hasReadPermission: true);
        await using var app = await CreateAppAsync(dependencies);
        using var client = app.GetTestClient();
        using var request = CreateSelectionRequest();

        using var response = await client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await Assert.That(response.Headers.Contains("Set-Cookie")).IsFalse();
        await dependencies.UserSiteService.DidNotReceive()
            .HasPermissionAsync(
                Arg.Any<long>(),
                Arg.Any<long>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ThemeUpdateAuthorizesRouteSiteInsteadOfForgedCurrentSiteCookie()
    {
        var dependencies = CreateDependencies(hasReadPermission: true);
        await using var app = await CreateAppAsync(dependencies);
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"/{HttpConstants.ApiPrefix}admin/sites/{SiteId}/theme")
        {
            Content = JsonContent.Create(new UpdateSiteThemeRequest(1, "aero-safe", "1.0.0"))
        };
        request.WithTestUser(UserId);
        request.Headers.Add("Cookie", "AeroCms.SiteId=999999");

        using var response = await client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await dependencies.UserSiteService.Received(1)
            .HasPermissionAsync(UserId, SiteId, "update", Arg.Any<CancellationToken>());
        await dependencies.ThemeSelectionService.Received(1)
            .UpdateAsync(SiteId, Arg.Any<UpdateSiteThemeRequest>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ThemeUpdateRejectsUnassignedRouteSiteBeforeMutation()
    {
        var dependencies = CreateDependencies(hasReadPermission: false);
        await using var app = await CreateAppAsync(dependencies);
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"/{HttpConstants.ApiPrefix}admin/sites/{SiteId}/theme")
        {
            Content = JsonContent.Create(new UpdateSiteThemeRequest(1, "aero-safe", "1.0.0"))
        };
        request.WithTestUser(UserId);

        using var response = await client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await dependencies.ThemeSelectionService.DidNotReceive()
            .UpdateAsync(Arg.Any<long>(), Arg.Any<UpdateSiteThemeRequest>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AdminRoleCanUpdateThemeWithoutSiteAssignment()
    {
        var dependencies = CreateDependencies(hasReadPermission: false);
        await using var app = await CreateAppAsync(dependencies);
        using var client = app.GetTestClient();
        using var request = CreateThemeUpdateRequest();
        request.WithTestUser(UserId, role: "Admin");

        using var response = await client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await dependencies.UserSiteService.DidNotReceive()
            .HasPermissionAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await dependencies.ThemeSelectionService.Received(1)
            .UpdateAsync(SiteId, Arg.Any<UpdateSiteThemeRequest>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExactIsAdminClaimCanUpdateThemeWithoutSiteAssignment()
    {
        var dependencies = CreateDependencies(hasReadPermission: false);
        await using var app = await CreateAppAsync(dependencies);
        using var client = app.GetTestClient();
        using var request = CreateThemeUpdateRequest();
        request.WithTestUser(UserId, isAdmin: true);

        using var response = await client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await dependencies.UserSiteService.DidNotReceive()
            .HasPermissionAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await dependencies.ThemeSelectionService.Received(1)
            .UpdateAsync(SiteId, Arg.Any<UpdateSiteThemeRequest>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SitesModuleDeclaresThemeModuleDependency()
    {
        await Assert.That(new SitesModule().Dependencies).Contains("AeroThemeModule");
    }

    private static HttpRequestMessage CreateSelectionRequest()
        => new(
            HttpMethod.Post,
            $"/{HttpConstants.ApiPrefix}admin/sites/current")
        {
            Content = JsonContent.Create(new SetCurrentSiteRequest(SiteId))
        };

    private static HttpRequestMessage CreateThemeUpdateRequest()
        => new(
            HttpMethod.Put,
            $"/{HttpConstants.ApiPrefix}admin/sites/{SiteId}/theme")
        {
            Content = JsonContent.Create(new UpdateSiteThemeRequest(1, "aero-safe", "1.0.0"))
        };

    private static TestDependencies CreateDependencies(bool hasReadPermission)
    {
        var siteLookup = Substitute.For<ISiteLookupService>();
        siteLookup.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<SiteViewModel>>(
            [
                new()
                {
                    Id = SiteId,
                    TenantId = 91,
                    Name = "Assigned site",
                    IsEnabled = true
                }
            ]));

        var userSiteService = Substitute.For<IUserSiteService>();
        userSiteService
            .HasPermissionAsync(
                Arg.Any<long>(),
                Arg.Any<long>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(hasReadPermission);

        var themeSelectionService = Substitute.For<ISiteThemeSelectionService>();
        themeSelectionService.UpdateAsync(
                Arg.Any<long>(),
                Arg.Any<UpdateSiteThemeRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult<Result<SiteThemeSelectionViewModel, AeroError>>(
                new Result<SiteThemeSelectionViewModel, AeroError>.Ok(
                    new SiteThemeSelectionViewModel(
                        call.ArgAt<UpdateSiteThemeRequest>(1).ThemeId,
                        call.ArgAt<UpdateSiteThemeRequest>(1).ThemeVersion,
                        2))));

        return new TestDependencies(
            siteLookup,
            userSiteService,
            Substitute.For<IMessageBus>(),
            themeSelectionService);
    }

    private static async Task<WebApplication> CreateAppAsync(TestDependencies dependencies)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddLogging();
        builder.Services.AddTestAuthentication();
        builder.Services.AddSingleton(dependencies.SiteLookup);
        builder.Services.AddSingleton(dependencies.UserSiteService);
        builder.Services.AddSingleton(dependencies.Bus);
        builder.Services.AddSingleton(dependencies.ThemeSelectionService);

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapSitesApi();
        await app.StartAsync();
        return app;
    }

    private sealed record TestDependencies(
        ISiteLookupService SiteLookup,
        IUserSiteService UserSiteService,
        IMessageBus Bus,
        ISiteThemeSelectionService ThemeSelectionService);
}
