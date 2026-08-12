using Aero.Cms.Shared.Localization;

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
}
