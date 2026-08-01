using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Modules.Footer.Domain;
using Aero.Cms.Modules.Footer.Events;
using Aero.Cms.Modules.Footer.Services;
using Aero.Cms.Modules.Navigation.Domain;
using Aero.Cms.Modules.Navigation.Events;
using Aero.Cms.Modules.Navigation.Services;
using Aero.Core;
using Aero.Core.Http;
using Aero.Core.Railway;
using AeroDB.Sable;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using System.Globalization;

namespace Aero.Cms.Core.Tests.Services;

public sealed class NavigationFooterServiceSiteIsolationTests
{
    [Test]
    public async Task Navigation_public_resolution_rejects_foreign_ids_and_selects_same_site_culture_variant()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<NavMenuDocument>(SchemaMode.Flexible)
            .WithSchema<SiteNavigationSettingsDocument>(SchemaMode.Flexible);
        await harness.InitializeAsync();
        SeedNavigation(harness.Session, 100, 1, "en-US", 100, "LOCAL");
        SeedNavigation(harness.Session, 101, 1, "fr-FR", 100, "FR");
        SeedNavigation(harness.Session, 200, 2, "fr-FR", 200, "FOREIGN");
        harness.Session.Store(new SiteNavigationSettingsDocument
        {
            Id = 1,
            SiteId = 1,
            DefaultNavMenuId = 200
        });
        await harness.Session.SaveChangesAsync();
        var service = CreateNavigationService(harness.Session, 1);
        var originalCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");
        try
        {
            var directForeign = Unwrap(await service.GetPublishedSnapshotAsync(1, 200));
            var foreignOverride = Unwrap(await service.ResolveSnapshotAsync(1, 200));
            var corruptDefault = Unwrap(await service.ResolveSnapshotAsync(1));
            var cultureVariant = Unwrap(await service.ResolveSnapshotAsync(1, 100));

            await Assert.That(directForeign).IsNull();
            await Assert.That(foreignOverride).IsNull();
            await Assert.That(corruptDefault).IsNull();
            await Assert.That(cultureVariant).IsNotNull();
            await Assert.That(cultureVariant!.SiteLogoUrl).IsEqualTo("FR");
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    [Test]
    public async Task Navigation_admin_service_does_not_disclose_or_mutate_foreign_menu()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<NavMenuDocument>(SchemaMode.Flexible);
        await harness.InitializeAsync();
        SeedNavigation(harness.Session, 200, 2, "en-US", 200, "FOREIGN");
        await harness.Session.SaveChangesAsync();
        var service = CreateNavigationService(harness.Session, 1);

        var get = await service.GetAsync(200);
        var save = await service.SaveDraftAsync(
            200,
            new UpdateNavigationRequest("Attacker", null, []),
            expectedVersion: 0);

        await Assert.That(get.IsFailure).IsTrue();
        await Assert.That(save.IsFailure).IsTrue();
        await using var verify = await harness.Store.QuerySessionAsync();
        await Assert.That((await verify.LoadAsync<NavMenuDocument>(200))!.Name)
            .IsEqualTo("Menu 200");
    }

    [Test]
    public async Task Footer_public_resolution_rejects_foreign_default_and_uses_same_site_culture_fallback()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<FooterDocument>(SchemaMode.Flexible)
            .WithSchema<SiteFooterSettingsDocument>(SchemaMode.Flexible);
        await harness.InitializeAsync();
        SeedFooter(harness.Session, 300, 1, "en-US", 300, "LOCAL");
        SeedFooter(harness.Session, 301, 1, "fr-FR", 300, "FR");
        SeedFooter(harness.Session, 400, 2, "fr-FR", 400, "FOREIGN");
        var settings = new SiteFooterSettingsDocument
        {
            Id = 1,
            SiteId = 1,
            DefaultFooterId = 400
        };
        harness.Session.Store(settings);
        await harness.Session.SaveChangesAsync();
        var service = CreateFooterService(harness.Session, 1);
        var originalCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");
        try
        {
            var directForeign = Unwrap(await service.GetPublishedSnapshotAsync(1, 400));
            var corruptDefault = Unwrap(await service.ResolveSnapshotAsync(1));
            await Assert.That(directForeign).IsNull();
            await Assert.That(corruptDefault).IsNull();

            harness.Session.Delete(settings);
            await harness.Session.SaveChangesAsync();
            var fallback = Unwrap(await service.ResolveSnapshotAsync(1));
            await Assert.That(fallback).IsNotNull();
            await Assert.That(fallback!.Brand.CompanyName).IsEqualTo("FR");
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    [Test]
    public async Task Footer_admin_service_does_not_disclose_or_mutate_foreign_footer()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<FooterDocument>(SchemaMode.Flexible);
        await harness.InitializeAsync();
        SeedFooter(harness.Session, 400, 2, "en-US", 400, "FOREIGN");
        await harness.Session.SaveChangesAsync();
        var service = CreateFooterService(harness.Session, 1);

        var get = await service.GetAsync(400);
        var save = await service.SaveDraftAsync(
            400,
            new UpdateFooterRequest("Attacker", null, "Attacker", []),
            expectedVersion: 0);

        await Assert.That(get.IsFailure).IsTrue();
        await Assert.That(save.IsFailure).IsTrue();
        await using var verify = await harness.Store.QuerySessionAsync();
        await Assert.That((await verify.LoadAsync<FooterDocument>(400))!.Name)
            .IsEqualTo("Footer 400");
    }

    private static void SeedNavigation(
        IDocumentSession session,
        long id,
        long siteId,
        string culture,
        long groupId,
        string marker)
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = new NavMenuSnapshot { SiteLogoUrl = marker };
        var created = new NavMenuCreated(siteId, $"Menu {id}", $"menu-{id}", null, now, culture, groupId);
        var draft = new NavMenuDraftSaved(siteId, $"Menu {id}", $"menu-{id}", snapshot, null, now, null);
        var published = new NavMenuPublished(siteId, snapshot, null, now, null);
        session.Events.StartStream(
            NavMenuStreams.Menu(id),
            new object[] { created, draft, published });
        var document = NavMenuDocument.Create(id, created);
        document.Apply(draft);
        document.Apply(published);
        session.Store(document);
    }

    private static void SeedFooter(
        IDocumentSession session,
        long id,
        long siteId,
        string culture,
        long groupId,
        string marker)
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = new FooterSnapshot
        {
            Brand = new FooterBrandSettings { CompanyName = marker }
        };
        var created = new FooterCreated(
            siteId,
            $"Footer {id}",
            $"footer-{id}",
            null,
            null,
            now,
            culture,
            groupId);
        var draft = new FooterDraftSaved(
            siteId,
            $"Footer {id}",
            $"footer-{id}",
            null,
            snapshot,
            null,
            now,
            null);
        var published = new FooterPublished(siteId, snapshot, null, now, null);
        session.Events.StartStream(
            FooterStreams.Footer(id),
            new object[] { created, draft, published });
        var document = FooterDocument.Create(id, created);
        document.Apply(draft);
        document.Apply(published);
        session.Store(document);
    }

    private static NavMenuService CreateNavigationService(IDocumentSession session, long siteId)
    {
        var site = Substitute.For<ISiteContext>();
        site.SiteId.Returns(siteId);
        return new NavMenuService(
            session,
            site,
            NullLogger<NavMenuService>.Instance);
    }

    private static FooterService CreateFooterService(IDocumentSession session, long siteId)
    {
        var site = Substitute.For<ISiteContext>();
        site.SiteId.Returns(siteId);
        return new FooterService(
            session,
            site,
            NullLogger<FooterService>.Instance);
    }

    private static T Unwrap<T>(Result<T, AeroError> result) =>
        result is Result<T, AeroError>.Ok ok
            ? ok.Value
            : throw new InvalidOperationException("Expected a successful result.");
}
