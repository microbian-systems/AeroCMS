using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Pages;

namespace Aero.Cms.Core.Tests.Localization;

public sealed class PageCultureForkerTests
{
    [Test]
    public async Task Fork_CreatesDraftCultureVariant_WithSameTranslationSet()
    {
        var source = new PageDocument
        {
            Id = 100,
            SiteId = 42,
            TranslationSetId = 900,
            Culture = "en-US",
            Slug = "about",
            Title = "About",
            Path = "/about",
            PublicationState = ContentPublicationState.Published,
            PublishedOn = DateTimeOffset.UtcNow,
            PublishedVersion = 7,
            Blocks =
            [
                new EditorBlock
                {
                    EditorId = "hero-source",
                    Type = "hero",
                    Title = "Welcome"
                }
            ],
            BlockIdMap = new Dictionary<string, long> { ["hero-source"] = 1234 }
        };

        var fork = PageCultureForker.Fork(source, 200, "es-mx", "acerca-de");

        await Assert.That(fork.Id).IsEqualTo(200);
        await Assert.That(fork.SiteId).IsEqualTo(42);
        await Assert.That(fork.TranslationSetId).IsEqualTo(900);
        await Assert.That(fork.Culture).IsEqualTo("es-MX");
        await Assert.That(fork.Slug).IsEqualTo("acerca-de");
        await Assert.That(fork.Path).IsEqualTo("/acerca-de");
        await Assert.That(fork.PublicationState).IsEqualTo(ContentPublicationState.Draft);
        await Assert.That(fork.PublishedOn).IsNull();
        await Assert.That(fork.PublishedVersion).IsEqualTo(0);
        await Assert.That(fork.BlockIdMap.Count).IsEqualTo(0);
        await Assert.That(fork.LayoutRegions.Count).IsEqualTo(0);
        await Assert.That(fork.Blocks.Count).IsEqualTo(1);
        await Assert.That(fork.Blocks[0].Title).IsEqualTo("Welcome");
        await Assert.That(fork.Blocks[0].EditorId).IsNotEqualTo("hero-source");
    }

    [Test]
    public async Task Fork_UsesSourceIdAsTranslationSet_WhenSourceHasNoTranslationSet()
    {
        var source = new PageDocument
        {
            Id = 100,
            SiteId = 42,
            Culture = "en-US",
            Slug = "about",
            Title = "About"
        };

        var fork = PageCultureForker.Fork(source, 200, "ar-SA", "/حول");

        await Assert.That(fork.TranslationSetId).IsEqualTo(100);
        await Assert.That(fork.Slug).IsEqualTo("حول");
        await Assert.That(fork.Path).IsEqualTo("/حول");
    }
}
