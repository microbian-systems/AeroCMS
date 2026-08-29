using Aero.Cms.Shared.Localization;
using Microsoft.AspNetCore.Http;

namespace Aero.Cms.Core.Tests.Content;

public sealed class AeroCultureRouteTests
{
    [Test]
    public async Task Canonicalizes_culture_casing_and_rejects_ambiguous_neutral_aliases()
    {
        await Assert.That(AeroCultureRoute.BuildCulturePath("en-us", "animal/husky")).IsEqualTo("/en-US/animal/husky");
        await Assert.That(AeroCultureRoute.TryResolveSupportedCultureAlias("en", ["en-US", "en-GB"], out _)).IsFalse();
        await Assert.That(AeroCultureRoute.TryResolveSupportedCultureAlias("en", ["en-US"], out var resolved)).IsTrue();
        await Assert.That(resolved).IsEqualTo("en-US");
    }

    [Test]
    public async Task Supported_culture_lookup_does_not_fabricate_en_us_when_the_site_does_not_configure_it()
    {
        await Assert.That(AeroCultureRoute.TryResolveSupportedCultureAlias("en-US", ["fr-FR"], out _)).IsFalse();
        await Assert.That(AeroCultureRoute.NormalizeSupportedCultures(["fr-FR"], string.Empty)).IsEquivalentTo(["fr-FR"]);
    }

    [Test]
    public async Task Canonical_route_generation_preserves_the_request_path_base()
    {
        var context = new DefaultHttpContext();
        context.Request.PathBase = "/tenant-a";

        await Assert.That(AeroCultureRoute.BuildCulturePathForCurrentRequest(context, "fr-fr", "animal/wolf"))
            .IsEqualTo("/tenant-a/fr-FR/animal/wolf");
    }
}
