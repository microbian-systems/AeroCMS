using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Pages.Composition;
using Aero.Cms.Core.Entities;
using Aero.Cms.Html;

namespace Aero.Cms.Core.Tests.Models;

public sealed class PageDocumentHtmlLifecycleTests
{
    [Test]
    public async Task ReplaceDraftContent_clones_input_and_increments_only_the_content_revision()
    {
        var input = CreateContent("First draft");
        var modifiedOn = new DateTimeOffset(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);
        var page = new PageDocument
        {
            ContentRevision = 4,
            PublishedVersion = 2,
            PublicationState = ContentPublicationState.Published
        };

        page.ReplaceDraftContent(input, modifiedOn);
        input.Root.Children[0].Children[0].Children[0].Text = "External mutation";

        await Assert.That(page.DraftContent.Root.Children[0].Children[0].Children[0].Text)
            .IsEqualTo("First draft");
        await Assert.That(page.DraftContent).IsNotSameReferenceAs(input);
        await Assert.That(page.ContentRevision).IsEqualTo(5);
        await Assert.That(page.PublishedVersion).IsEqualTo(2);
        await Assert.That(page.PublicationState).IsEqualTo(ContentPublicationState.Published);
        await Assert.That(page.ModifiedOn).IsEqualTo(modifiedOn);
    }

    [Test]
    public async Task ReplaceDraftContent_clones_html_and_composition_as_one_snapshot()
    {
        var input = CreateContent("Bound draft");
        var scopeNodeId = input.Root.Children[0].NodeId;
        var targetNodeId = input.Root.Children[0].Children[0].NodeId;
        var filters = new[]
        {
            new PageContentFilter
            {
                FieldName = "category",
                Operator = PageContentFilterOperator.Equals,
                Value = "news"
            }
        };
        var composition = new PageCompositionDocument
        {
            ContentLists =
            [
                new PageContentListScope
                {
                    NodeId = scopeNodeId,
                    ContentTypeId = 101,
                    ContentTypeAlias = "articles",
                    TemplateRootNodeId = targetNodeId,
                    Query = new PageContentListQuery { Filters = filters }
                }
            ]
        };
        var page = new PageDocument();

        page.ReplaceDraftContent(input, composition, DateTimeOffset.UtcNow);
        filters[0] = filters[0] with { Value = "changed" };

        await Assert.That(page.DraftComposition).IsNotSameReferenceAs(composition);
        await Assert.That(page.DraftComposition.ContentLists).IsNotSameReferenceAs(composition.ContentLists);
        await Assert.That(page.DraftComposition.ContentLists[0].Query.Filters[0].Value)
            .IsEqualTo("news");
    }

    [Test]
    public async Task PublishDraftContent_creates_an_independent_snapshot_and_updates_publication_metadata()
    {
        var publishedOn = new DateTimeOffset(2026, 7, 13, 13, 0, 0, TimeSpan.Zero);
        var page = new PageDocument
        {
            DraftContent = CreateContent("Publish me"),
            DraftComposition = new PageCompositionDocument
            {
                ContentItems =
                [
                    new PageContentItemScope
                    {
                        NodeId = 10,
                        ContentTypeId = 20,
                        ContentTypeAlias = "people",
                        ContentItemId = 30
                    }
                ]
            },
            ContentRevision = 7,
            PublishedVersion = 3,
            PublicationState = ContentPublicationState.Draft
        };

        page.PublishDraftContent(publishedOn);
        page.DraftContent.Root.Children[0].Children[0].Children[0].Text = "Later draft";
        page.DraftComposition = new PageCompositionDocument();

        await Assert.That(page.PublishedContent).IsNotNull();
        await Assert.That(page.PublishedContent!.Root.Children[0].Children[0].Children[0].Text)
            .IsEqualTo("Publish me");
        await Assert.That(page.PublishedContent).IsNotSameReferenceAs(page.DraftContent);
        await Assert.That(page.PublishedComposition).IsNotNull();
        await Assert.That(page.PublishedComposition!.ContentItems[0].ContentItemId).IsEqualTo(30);
        await Assert.That(page.PublishedComposition).IsNotSameReferenceAs(page.DraftComposition);
        await Assert.That(page.ContentRevision).IsEqualTo(7);
        await Assert.That(page.PublishedVersion).IsEqualTo(4);
        await Assert.That(page.PublicationState).IsEqualTo(ContentPublicationState.Published);
        await Assert.That(page.PublishedOn).IsEqualTo(publishedOn);
        await Assert.That(page.ModifiedOn).IsEqualTo(publishedOn);
    }

    [Test]
    public async Task UnpublishContent_preserves_the_last_published_snapshot_and_version()
    {
        var page = new PageDocument
        {
            DraftContent = CreateContent("Published"),
            PublishedVersion = 2
        };
        page.PublishDraftContent(DateTimeOffset.UtcNow);
        var snapshot = page.PublishedContent;
        var version = page.PublishedVersion;
        var modifiedOn = new DateTimeOffset(2026, 7, 13, 14, 0, 0, TimeSpan.Zero);

        page.UnpublishContent(modifiedOn);

        await Assert.That(page.PublicationState).IsEqualTo(ContentPublicationState.Draft);
        await Assert.That(page.PublishedOn).IsNull();
        await Assert.That(page.PublishedContent).IsSameReferenceAs(snapshot);
        await Assert.That(page.PublishedVersion).IsEqualTo(version);
        await Assert.That(page.ModifiedOn).IsEqualTo(modifiedOn);
    }

    private static HtmlPageContent CreateContent(string headingText)
    {
        var section = HtmlNode.CreateElement("section");
        var heading = HtmlNode.CreateElement("h2");
        heading.Children.Add(HtmlNode.CreateText(headingText));
        section.Children.Add(heading);
        var content = new HtmlPageContent();
        content.Root.Children.Add(section);
        return content;
    }
}
