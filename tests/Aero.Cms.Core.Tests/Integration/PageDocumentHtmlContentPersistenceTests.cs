using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Core.Entities;
using Aero.Cms.Html;
using AeroDB.Sable;

namespace Aero.Cms.Core.Tests.Integration;

public sealed class PageDocumentHtmlContentPersistenceTests
{
    [Test]
    public async Task Flexible_page_document_round_trips_independent_draft_and_published_html_snapshots()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<PageDocument>(SchemaMode.Flexible);
        await harness.InitializeAsync();

        var draft = CreateContent("Draft heading");
        var published = HtmlTreeOperations.ClonePreservingNodeIds(draft);
        published.Root.Children[0].Children[0].Children[0].Text = "Published heading";
        var page = new PageDocument
        {
            Id = 9_101,
            SiteId = 7,
            Title = "HTML aggregate persistence",
            Slug = "html-aggregate",
            PublicationState = ContentPublicationState.Published,
            DraftContent = draft,
            PublishedContent = published,
            ContentRevision = 4,
            PublishedVersion = 3
        };

        harness.Session.Store(page);
        await harness.Session.SaveChangesAsync();

        await using var verificationSession = await harness.OpenSessionAsync();
        var restored = await verificationSession.LoadAsync<PageDocument>(page.Id);

        await Assert.That(restored).IsNotNull();
        await Assert.That(restored!.DraftContent.Root.Children[0].Children[0].Children[0].Text)
            .IsEqualTo("Draft heading");
        await Assert.That(restored.PublishedContent!.Root.Children[0].Children[0].Children[0].Text)
            .IsEqualTo("Published heading");
        await Assert.That(restored.DraftContent.Root.Children[0].Style!.GridColumns).IsEqualTo(2);
        await Assert.That(restored.PublishedContent.Root.Children[0].Style!.Surface!.BackgroundColor!.Value)
            .IsEqualTo("surface.page");
        await Assert.That(restored.ContentRevision).IsEqualTo(4);
        await Assert.That(restored.PublishedVersion).IsEqualTo(3);
    }

    private static HtmlPageContent CreateContent(string headingText)
    {
        var section = HtmlNode.CreateElement("section");
        section.Style = new HtmlStyle
        {
            Display = CssDisplay.Grid,
            GridColumns = 2,
            Gap = CssLength.Rem(1.5m),
            Surface = new CssSurfaceStyle
            {
                BackgroundColor = CssColor.Token("surface.page")
            }
        };
        var heading = HtmlNode.CreateElement("h2");
        heading.Children.Add(HtmlNode.CreateText(headingText));
        section.Children.Add(heading);

        var content = new HtmlPageContent();
        content.Root.Children.Add(section);
        return content;
    }
}
