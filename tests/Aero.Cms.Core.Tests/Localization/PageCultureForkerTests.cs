using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Pages.Composition;
using Aero.Cms.Core.Entities;
using Aero.Cms.Html;
using Aero.Cms.Modules.Pages;
using Shouldly;

namespace Aero.Cms.Core.Tests.Localization;

public sealed class PageCultureForkerTests
{
    [Test]
    public async Task Fork_CreatesDraftCultureVariant_WithSameTranslationSet()
    {
        var content = new HtmlPageContent();
        var paragraph = HtmlNode.CreateElement("p");
        paragraph.Children.Add(HtmlNode.CreateText("Welcome"));
        content.Root.Children.Add(paragraph);

        var source = new PageDocument
        {
            Id = 100,
            SiteId = 42,
            TranslationGroupId = 900,
            Culture = "en-US",
            Slug = "about",
            Title = "About",
            Path = "/about",
            DraftRouteTemplate = "/catalog/{entryId}",
            PublicationState = ContentPublicationState.Published,
            PublishedOn = DateTimeOffset.UtcNow,
            PublishedVersion = 7,
            DraftContent = content,
            DraftComposition = new PageCompositionDocument
            {
                ContentItems =
                [
                    new PageContentItemScope
                    {
                        NodeId = paragraph.NodeId,
                        ContentTypeId = 501,
                        ContentTypeAlias = "welcome-messages",
                        ContentItemId = 601,
                        Slug = "welcome"
                    }
                ]
            },
            PublishedContent = HtmlTreeOperations.ClonePreservingNodeIds(content),
            PublishedComposition = new PageCompositionDocument(),
            ContentRevision = 3
        };

        var fork = PageCultureForker.Fork(source, 200, "es-mx", "acerca-de");

        await Assert.That(fork.Id).IsEqualTo(200);
        await Assert.That(fork.SiteId).IsEqualTo(42);
        await Assert.That(fork.TranslationGroupId).IsEqualTo(900);
        await Assert.That(fork.Culture).IsEqualTo("es-MX");
        await Assert.That(fork.Slug).IsEqualTo("acerca-de");
        await Assert.That(fork.Path).IsEqualTo("/acerca-de");
        fork.DraftRouteTemplate.ShouldBe("/catalog/{entryId}");
        await Assert.That(fork.PublicationState).IsEqualTo(ContentPublicationState.Draft);
        await Assert.That(fork.PublishedOn).IsNull();
        await Assert.That(fork.PublishedVersion).IsEqualTo(0);
        await Assert.That(fork.PublishedContent).IsNull();
        await Assert.That(fork.PublishedComposition).IsNull();
        await Assert.That(fork.DraftSourceVersionId).IsNull();
        await Assert.That(fork.PublishedSourceVersionId).IsNull();
        await Assert.That(fork.ContentRevision).IsEqualTo(3);
        await Assert.That(fork.DraftContent.Root.Children[0].Children[0].Text).IsEqualTo("Welcome");
        await Assert.That(fork.DraftContent).IsNotSameReferenceAs(source.DraftContent);
        await Assert.That(fork.DraftComposition).IsNotSameReferenceAs(source.DraftComposition);
        await Assert.That(fork.DraftComposition.ContentItems[0].ContentItemId).IsEqualTo(601);
        await Assert.That(fork.DraftContent.Root.Children[0].NodeId)
            .IsEqualTo(source.DraftContent.Root.Children[0].NodeId);
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
