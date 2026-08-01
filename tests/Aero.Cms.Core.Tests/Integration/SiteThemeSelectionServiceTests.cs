using Aero.Cms.Abstractions.Events;
using Aero.Cms.Abstractions.Requests;
using Aero.Cms.Abstractions.Theming;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Sites;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;
using NSubstitute;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Aero.Cms.Core.Tests.Integration;

public sealed class SiteThemeSelectionServiceTests
{
    [Test]
    public async Task Semantic_no_op_keeps_revision_and_does_not_publish()
    {
        await using var harness = await CreateHarnessAsync();
        var bus = Substitute.For<IMessageBus>();
        var service = CreateService(harness, bus);

        var result = await service.UpdateAsync(91, new UpdateSiteThemeRequest(2, "aero-safe", "1.0.0"));

        var ok = result as Result<Aero.Cms.Abstractions.Models.SiteThemeSelectionViewModel, AeroError>.Ok;
        await Assert.That(ok).IsNotNull();
        await Assert.That(ok!.Value.ThemeRevision).IsEqualTo(2);
        await bus.DidNotReceive().PublishAsync(Arg.Any<SiteThemeChangedEvent>());
    }

    [Test]
    public async Task Exact_installed_change_increments_once_persists_and_publishes_post_commit_event()
    {
        await using var harness = await CreateHarnessAsync();
        var bus = Substitute.For<IMessageBus>();
        var service = CreateService(harness, bus);

        var result = await service.UpdateAsync(91, new UpdateSiteThemeRequest(2, "ocean", "2.1.0"));

        var ok = result as Result<Aero.Cms.Abstractions.Models.SiteThemeSelectionViewModel, AeroError>.Ok;
        await Assert.That(ok).IsNotNull();
        await Assert.That(ok!.Value.ThemeRevision).IsEqualTo(3);
        await bus.Received(1).PublishAsync(Arg.Is<SiteThemeChangedEvent>(changed =>
            changed.SiteId == 91 && changed.ThemeId == "ocean" &&
            changed.ThemeVersion == "2.1.0" && changed.Revision == 3));

        await using var verification = await harness.OpenSessionAsync();
        var saved = (await verification.Query<SitesModel>().ToListAsync()).Single(site => site.Id == 91);
        await Assert.That(saved.ThemeId).IsEqualTo("ocean");
        await Assert.That(saved.ThemeVersion).IsEqualTo("2.1.0");
        await Assert.That(saved.ThemeRevision).IsEqualTo(3);
    }

    [Test]
    public async Task Missing_exact_version_and_stale_revision_fail_without_publish()
    {
        await using var harness = await CreateHarnessAsync();
        var bus = Substitute.For<IMessageBus>();
        var service = CreateService(harness, bus);

        var missing = await service.UpdateAsync(91, new UpdateSiteThemeRequest(2, "ocean", "2.0.0"));
        var stale = await service.UpdateAsync(91, new UpdateSiteThemeRequest(1, "ocean", "2.1.0"));

        await Assert.That(((Result<Aero.Cms.Abstractions.Models.SiteThemeSelectionViewModel, AeroError>.Failure)missing).Error)
            .IsTypeOf<AeroError.Validation>();
        await Assert.That(((Result<Aero.Cms.Abstractions.Models.SiteThemeSelectionViewModel, AeroError>.Failure)stale).Error)
            .IsTypeOf<AeroError.Conflict>();
        await bus.DidNotReceive().PublishAsync(Arg.Any<SiteThemeChangedEvent>());
    }

    [Test]
    public async Task Publication_failure_after_durable_save_is_logged_and_returns_success()
    {
        await using var harness = await CreateHarnessAsync();
        var bus = Substitute.For<IMessageBus>();
        bus.PublishAsync(Arg.Any<SiteThemeChangedEvent>())
            .Returns(_ => throw new InvalidOperationException("broker unavailable"));
        var service = CreateService(harness, bus);

        var result = await service.UpdateAsync(91, new UpdateSiteThemeRequest(2, "ocean", "2.1.0"));

        var ok = result as Result<Aero.Cms.Abstractions.Models.SiteThemeSelectionViewModel, AeroError>.Ok;
        await Assert.That(ok).IsNotNull();
        await Assert.That(ok!.Value.ThemeRevision).IsEqualTo(3);

        await using var verification = await harness.OpenSessionAsync();
        var saved = (await verification.Query<SitesModel>().ToListAsync()).Single(site => site.Id == 91);
        await Assert.That(saved.ThemeId).IsEqualTo("ocean");
        await Assert.That(saved.ThemeRevision).IsEqualTo(3);
    }

    private static SiteThemeSelectionService CreateService(SableTestHarness harness, IMessageBus bus)
        => new(
            harness.Store,
            CreateCatalog(),
            bus,
            Substitute.For<ILogger<SiteThemeSelectionService>>());

    private static IThemeCatalog CreateCatalog()
    {
        var catalog = Substitute.For<IThemeCatalog>();
        catalog.Find("aero-safe", "1.0.0").Returns(new InstalledThemeManifest(
            "aero-safe", "1.0.0", "Safe", "Tests", "", ThemeAuthoringEngine.Css,
            [new ThemeStylesheetAsset("/_content/Aero.Cms.Modules.Theming/themes/aero-safe/1.0.0/theme.css", 0)],
            IsSafeDefault: true));
        catalog.Find("ocean", "2.1.0").Returns(new InstalledThemeManifest(
            "ocean", "2.1.0", "Ocean", "Tests", "", ThemeAuthoringEngine.Css,
            [new ThemeStylesheetAsset("/_content/Aero.Cms.Modules.Theming/themes/ocean/2.1.0/theme.css", 0)]));
        return catalog;
    }

    private static async Task<SableTestHarness> CreateHarnessAsync()
    {
        var harness = new SableTestHarness()
            .WithSchema<SitesModel>(SchemaMode.Strict)
            .WithConfiguration(options => options.Schema.For<SitesModel>().UseOptimisticConcurrency = true);
        await harness.InitializeAsync();
        harness.Session.Store(new SitesModel
        {
            Id = 91,
            TenantId = 7,
            Name = "Theme Test",
            IsEnabled = true,
            ThemeId = "aero-safe",
            ThemeVersion = "1.0.0",
            ThemeRevision = 2
        });
        await harness.Session.SaveChangesAsync();
        return harness;
    }
}
