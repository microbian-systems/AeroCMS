using System.Net;
using System.Text.Json;
using Aero.Cms.Modules.Footer.Areas.Api.v1;
using Aero.Cms.Modules.Footer.Domain;
using Aero.Cms.Modules.Footer.Events;
using Aero.Cms.Modules.Footer.Services;
using Aero.Cms.Modules.Navigation.Areas.Api.v1;
using Aero.Cms.Modules.Navigation.Domain;
using Aero.Cms.Modules.Navigation.Events;
using Aero.Cms.Modules.Navigation.Services;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Aero.Cms.Core.Tests.Integration;

public sealed class NavigationFooterEventHistoryScopeTests
{
    [Test]
    [Arguments("navigation")]
    [Arguments("footer")]
    public async Task Event_history_preflights_site_ownership_before_returning_stream(string module)
    {
        await using var harness = new SableTestHarness();
        await harness.InitializeAsync();
        SeedStreams(harness.Session);
        await harness.Session.SaveChangesAsync();
        var navigation = Substitute.For<INavMenuService>();
        navigation.GetAsync(1, Arg.Any<CancellationToken>())
            .Returns(Ok(new NavMenuDocument { Id = 1, SiteId = 1, Name = "Local" }));
        navigation.GetAsync(2, Arg.Any<CancellationToken>())
            .Returns(Fail<NavMenuDocument>());
        var footer = Substitute.For<IFooterService>();
        footer.GetAsync(1, Arg.Any<CancellationToken>())
            .Returns(Ok(new FooterDocument { Id = 1, SiteId = 1, Name = "Local" }));
        footer.GetAsync(2, Arg.Any<CancellationToken>())
            .Returns(Fail<FooterDocument>());
        await using var app = await CreateAppAsync(harness.Session, navigation, footer);
        var prefix = module == "navigation" ? "navigations" : "footers";

        using var local = await app.GetTestClient().SendAsync(
            new HttpRequestMessage(HttpMethod.Get, $"/api/v1/admin/{prefix}/1/events")
                .WithTestUser(8512));
        using var foreign = await app.GetTestClient().SendAsync(
            new HttpRequestMessage(HttpMethod.Get, $"/api/v1/admin/{prefix}/2/events")
                .WithTestUser(8512));

        await Assert.That(local.StatusCode).IsEqualTo(HttpStatusCode.OK);
        using var payload = JsonDocument.Parse(await local.Content.ReadAsStringAsync());
        await Assert.That(payload.RootElement.GetProperty("totalEvents").GetInt32()).IsEqualTo(1);
        await Assert.That(foreign.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    private static void SeedStreams(IDocumentSession session)
    {
        var now = DateTimeOffset.UtcNow;
        session.Events.StartStream(
            NavMenuStreams.Menu(1),
            new object[] { new NavMenuCreated(1, "Local", "local", null, now) });
        session.Events.StartStream(
            NavMenuStreams.Menu(2),
            new object[] { new NavMenuCreated(2, "Foreign", "foreign", null, now) });
        session.Events.StartStream(
            FooterStreams.Footer(1),
            new object[] { new FooterCreated(1, "Local", "local", null, null, now) });
        session.Events.StartStream(
            FooterStreams.Footer(2),
            new object[] { new FooterCreated(2, "Foreign", "foreign", null, null, now) });
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
        builder.Services.AddSingleton(query);
        builder.Services.AddSingleton(navigation);
        builder.Services.AddSingleton(footer);
        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapNavigationAdminApi();
        app.MapFooterAdminApi();
        await app.StartAsync();
        return app;
    }

    private static Task<Result<T, AeroError>> Ok<T>(T value) =>
        Task.FromResult<Result<T, AeroError>>(new Result<T, AeroError>.Ok(value));

    private static Task<Result<T, AeroError>> Fail<T>() =>
        Task.FromResult<Result<T, AeroError>>(
            AeroError.NotFoundError("Not found."));
}
