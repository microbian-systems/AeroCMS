using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Pages;
using System.Text.Json;

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
            TranslationGroupId = 900,
            Culture = "en-US",
            Slug = "about",
            Title = "About",
            Path = "/about",
            PublicationState = ContentPublicationState.Published,
            PublishedOn = DateTimeOffset.UtcNow,
            PublishedVersion = 7,
            RootNodes =
            [
                new NeoPageNode
                {
                    NodeId = "hero-source",
                    CatalogId = "hero",
                    Properties = new Dictionary<string, JsonElement>
                    {
                        ["title"] = JsonSerializer.SerializeToElement("Welcome")
                    }
                }
            ],
            BlockIdMap = new Dictionary<string, long> { ["hero-source"] = 1234 }
        };

        var fork = PageCultureForker.Fork(source, 200, "es-mx", "acerca-de");

        await Assert.That(fork.Id).IsEqualTo(200);
        await Assert.That(fork.SiteId).IsEqualTo(42);
        await Assert.That(fork.TranslationGroupId).IsEqualTo(900);
        await Assert.That(fork.Culture).IsEqualTo("es-MX");
        await Assert.That(fork.Slug).IsEqualTo("acerca-de");
        await Assert.That(fork.Path).IsEqualTo("/acerca-de");
        await Assert.That(fork.PublicationState).IsEqualTo(ContentPublicationState.Draft);
        await Assert.That(fork.PublishedOn).IsNull();
        await Assert.That(fork.PublishedVersion).IsEqualTo(0);
        await Assert.That(fork.BlockIdMap.Count).IsEqualTo(0);
        await Assert.That(fork.LayoutRegions.Count).IsEqualTo(0);
        await Assert.That(fork.RootNodes.Count).IsEqualTo(1);
        await Assert.That(fork.RootNodes[0].Properties["title"].GetString()).IsEqualTo("Welcome");
        await Assert.That(fork.RootNodes[0].NodeId).IsNotEqualTo("hero-source");
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

        await Assert.That(fork.TranslationGroupId).IsEqualTo(100);
        await Assert.That(fork.Slug).IsEqualTo("حول");
        await Assert.That(fork.Path).IsEqualTo("/حول");
    }
}
