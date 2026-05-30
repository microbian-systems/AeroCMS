using Aero.Cms.Modules.Footer;
using Aero.Cms.Modules.Footer.Domain;

namespace Aero.Cms.Modules.Footer.Tests;

public sealed class FooterCultureForkerTests
{
    [Test]
    public async Task Fork_CreatesCultureVariantEvents_WithSameTranslationSet()
    {
        var source = new FooterDocument
        {
            Id = 100,
            SiteId = 42,
            TranslationSetId = 900,
            Culture = "en-US",
            Name = "Footer",
            Key = "footer",
            Description = "Source footer"
        };
        var snapshot = new FooterSnapshot
        {
            Brand = new FooterBrandSettings
            {
                CompanyName = "Aero",
                Tagline = "Source tagline"
            },
            Sections =
            [
                new FooterLinkGroup
                {
                    Key = "company",
                    Title = "Company",
                    Links = [new FooterLink("About", "/about")]
                }
            ]
        };
        var timestamp = new DateTimeOffset(2026, 5, 30, 12, 0, 0, TimeSpan.Zero);

        var fork = FooterCultureForker.Fork(source, snapshot, 200, "es-mx", userId: 7, timestamp);

        await Assert.That(fork.Created.SiteId).IsEqualTo(42);
        await Assert.That(fork.Created.Culture).IsEqualTo("es-MX");
        await Assert.That(fork.Created.TranslationSetId).IsEqualTo(900);
        await Assert.That(fork.Created.UserId).IsEqualTo(7);
        await Assert.That(fork.DraftSaved.Snapshot.Sections).Count().IsEqualTo(1);
        await Assert.That(fork.DraftSaved.Snapshot.Sections[0]).IsAssignableTo<FooterLinkGroup>();
        var group = (FooterLinkGroup)fork.DraftSaved.Snapshot.Sections[0];
        await Assert.That(group.Title).IsEqualTo("Company");
        await Assert.That(group.Links[0].Href).IsEqualTo("/about");
        await Assert.That(ReferenceEquals(snapshot.Sections, fork.DraftSaved.Snapshot.Sections)).IsFalse();
        await Assert.That(ReferenceEquals(((FooterLinkGroup)snapshot.Sections[0]).Links, group.Links)).IsFalse();
    }

    [Test]
    public async Task Fork_UsesSourceIdAsTranslationSet_WhenSourceHasNoTranslationSet()
    {
        var source = new FooterDocument
        {
            Id = 100,
            SiteId = 42,
            Culture = "en-US",
            Name = "Footer",
            Key = "footer"
        };

        var fork = FooterCultureForker.Fork(source, FooterSnapshot.Empty, 200, "ar-SA");

        await Assert.That(fork.Created.TranslationSetId).IsEqualTo(100);
        await Assert.That(fork.Created.Culture).IsEqualTo("ar-SA");
    }
}
