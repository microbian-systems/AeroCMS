using Aero.Cms.Shared.Localization;
using Microsoft.AspNetCore.Http;

namespace Aero.Cms.Core.Tests.Localization;

public sealed class AeroCultureRouteTests
{
    [Test]
    public async Task StripLeadingCulture_RemovesCulturePrefix()
    {
        var slug = AeroCultureRoute.StripLeadingCulture("/es-mx/acerca-de");

        await Assert.That(slug).IsEqualTo("acerca-de");
    }

    [Test]
    public async Task ResolveRequestCulture_UsesSupportedUrlCulture()
    {
        var culture = AeroCultureRoute.ResolveRequestCulture(
            new PathString("/es-mx/acerca-de"),
            "en-US",
            ["en-US", "es-MX"],
            out var pathCulture);

        await Assert.That(culture).IsEqualTo("es-MX");
        await Assert.That(pathCulture).IsEqualTo("es-MX");
    }

    [Test]
    public async Task ResolveRequestCulture_FallsBackToSiteDefaultWhenUrlCultureIsUnsupported()
    {
        var culture = AeroCultureRoute.ResolveRequestCulture(
            new PathString("/fr-fr/a-propos"),
            "en-US",
            ["en-US", "es-MX"],
            out var pathCulture);

        await Assert.That(culture).IsEqualTo("en-US");
        await Assert.That(pathCulture).IsNull();
    }
}
