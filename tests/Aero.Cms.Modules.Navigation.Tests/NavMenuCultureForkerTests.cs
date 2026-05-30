using Aero.Cms.Modules.Navigation;
using Aero.Cms.Modules.Navigation.Domain;

namespace Aero.Cms.Modules.Navigation.Tests;

public sealed class NavMenuCultureForkerTests
{
    [Test]
    public async Task Fork_CreatesCultureVariantEvents_WithSameTranslationSet()
    {
        var source = new NavMenuDocument
        {
            Id = 100,
            SiteId = 42,
            TranslationSetId = 900,
            Culture = "en-US",
            Name = "Header",
            Key = "main"
        };
        var snapshot = new NavMenuSnapshot
        {
            Left =
            [
                new NavLink
                {
                    Key = "about",
                    Label = "About",
                    Href = "/about"
                }
            ]
        };
        var timestamp = new DateTimeOffset(2026, 5, 30, 12, 0, 0, TimeSpan.Zero);

        var fork = NavMenuCultureForker.Fork(source, snapshot, 200, "es-mx", userId: 7, timestamp);

        await Assert.That(fork.Created.SiteId).IsEqualTo(42);
        await Assert.That(fork.Created.Culture).IsEqualTo("es-MX");
        await Assert.That(fork.Created.TranslationSetId).IsEqualTo(900);
        await Assert.That(fork.Created.UserId).IsEqualTo(7);
        await Assert.That(fork.DraftSaved.Snapshot.Left).Count().IsEqualTo(1);
        await Assert.That(fork.DraftSaved.Snapshot.Left[0]).IsAssignableTo<NavLink>();
        await Assert.That(((NavLink)fork.DraftSaved.Snapshot.Left[0]).Label).IsEqualTo("About");
        await Assert.That(ReferenceEquals(snapshot.Left, fork.DraftSaved.Snapshot.Left)).IsFalse();
    }

    [Test]
    public async Task Fork_UsesSourceIdAsTranslationSet_WhenSourceHasNoTranslationSet()
    {
        var source = new NavMenuDocument
        {
            Id = 100,
            SiteId = 42,
            Culture = "en-US",
            Name = "Header",
            Key = "main"
        };

        var fork = NavMenuCultureForker.Fork(source, NavMenuSnapshot.Empty, 200, "ar-SA");

        await Assert.That(fork.Created.TranslationSetId).IsEqualTo(100);
        await Assert.That(fork.Created.Culture).IsEqualTo("ar-SA");
    }
}
