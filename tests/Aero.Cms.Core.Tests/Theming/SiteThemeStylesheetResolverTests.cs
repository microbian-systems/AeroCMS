using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Modules.Theming;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Aero.Cms.Core.Tests.Theming;

public sealed class SiteThemeStylesheetResolverTests
{
    [Test]
    public async Task Resolver_returns_exact_local_assets_for_current_site()
    {
        var catalog = new DeploymentThemeCatalog([
            DeploymentThemeCatalogTests.CreateManifest("aero-safe", "1.0.0", true),
            DeploymentThemeCatalogTests.CreateManifest("ocean", "2.1.0", false)
        ]);
        var context = new DefaultHttpContext();
        context.Features.Set<IAeroSiteSlice>(new AeroSiteSlice
        {
            SiteId = 42,
            TenantId = 7,
            ThemeId = "ocean",
            ThemeVersion = "2.1.0",
            ThemeRevision = 8
        });
        var accessor = new HttpContextAccessor { HttpContext = context };
        var resolver = new SiteThemeStylesheetResolver(
            accessor,
            catalog,
            Substitute.For<ILogger<SiteThemeStylesheetResolver>>());

        var resolved = await resolver.ResolveAsync();

        await Assert.That(resolved.UsedSafeDefault).IsFalse();
        await Assert.That(resolved.ThemeId).IsEqualTo("ocean");
        await Assert.That(resolved.ThemeVersion).IsEqualTo("2.1.0");
        await Assert.That(resolved.ThemeRevision).IsEqualTo(8);
        await Assert.That(resolved.Stylesheets.Single().Path).StartsWith("/_content/");
    }

    [Test]
    public async Task Resolver_fails_closed_without_rewriting_missing_selection()
    {
        var catalog = new DeploymentThemeCatalog([
            DeploymentThemeCatalogTests.CreateManifest("aero-safe", "1.0.0", true)
        ]);
        var slice = new AeroSiteSlice
        {
            SiteId = 42,
            TenantId = 7,
            ThemeId = "removed-theme",
            ThemeVersion = "9.9.9",
            ThemeRevision = 12
        };
        var context = new DefaultHttpContext();
        context.Features.Set<IAeroSiteSlice>(slice);
        var resolver = new SiteThemeStylesheetResolver(
            new HttpContextAccessor { HttpContext = context },
            catalog,
            Substitute.For<ILogger<SiteThemeStylesheetResolver>>());

        var resolved = await resolver.ResolveAsync();

        await Assert.That(resolved.UsedSafeDefault).IsTrue();
        await Assert.That(resolved.ThemeId).IsEqualTo("aero-safe");
        await Assert.That(resolved.ThemeRevision).IsEqualTo(12);
        await Assert.That(slice.ThemeId).IsEqualTo("removed-theme");
        await Assert.That(slice.ThemeVersion).IsEqualTo("9.9.9");
    }
}
