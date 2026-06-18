using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Events;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Core.Entities;

namespace Aero.Cms.Core.Tests.Models;

public sealed class PageDocumentPublicationTests
{
    [Test]
    public async Task ApplyPagePublished_MarksPagePublicAndIncrementsVersion()
    {
        var page = new PageDocument
        {
            Id = 100,
            PublicationState = ContentPublicationState.Draft,
            PublishedVersion = 2
        };

        page.Apply(new PagePublished(PageId: page.Id, Version: 3));

        await Assert.That(page.PublicationState).IsEqualTo(ContentPublicationState.Published);
        await Assert.That(page.PublishedOn).IsNotNull();
        await Assert.That(page.PublishedVersion).IsEqualTo(3);
        await Assert.That(page.IsPubliclyVisible).IsTrue();
    }

    [Test]
    public async Task ApplyPageCompositionPublished_MarksPagePublicAndPreservesTree()
    {
        var page = new PageDocument
        {
            Id = 100,
            Culture = "en-US",
            PublicationState = ContentPublicationState.Draft,
            PublishedVersion = 2
        };

        var root = new NeoPageNode
        {
            NodeId = "section-1",
            CatalogId = "section",
            Kind = NeoPageNodeKind.Section
        };

        page.Apply(new PageCompositionPublished(
            PageId: page.Id,
            SiteId: page.SiteId,
            PublishedCompositionId: 200,
            PublishedVersion: 3,
            Culture: "es-MX",
            Title: "Publicado",
            Slug: "publicado",
            Summary: null,
            SeoTitle: null,
            SeoDescription: null,
            RootNodes: [root]));

        await Assert.That(page.PublicationState).IsEqualTo(ContentPublicationState.Published);
        await Assert.That(page.PublishedOn).IsNotNull();
        await Assert.That(page.PublishedVersion).IsEqualTo(3);
        await Assert.That(page.Culture).IsEqualTo("es-MX");
        await Assert.That(page.RootNodes).Count().IsEqualTo(1);
        await Assert.That(page.RootNodes[0].NodeId).IsEqualTo("section-1");
        await Assert.That(page.RootNodes[0]).IsNotSameReferenceAs(root);
        await Assert.That(page.IsPubliclyVisible).IsTrue();
    }

    [Test]
    public async Task ApplyPageStateChanged_DraftClearsPublishedVisibility()
    {
        var page = new PageDocument
        {
            PublicationState = ContentPublicationState.Published,
            PublishedOn = DateTimeOffset.UtcNow,
            PublishedVersion = 4
        };

        page.Apply(new PageStateChanged(ContentPublicationState.Draft));

        await Assert.That(page.PublicationState).IsEqualTo(ContentPublicationState.Draft);
        await Assert.That(page.PublishedOn).IsNull();
        await Assert.That(page.PublishedVersion).IsEqualTo(4);
        await Assert.That(page.IsPubliclyVisible).IsFalse();
    }

    [Test]
    public async Task ToViewModel_PreservesPublicationState()
    {
        var page = new PageDocument
        {
            PublicationState = ContentPublicationState.InReview
        };

        var viewModel = page.ToViewModel();

        await Assert.That(viewModel.PublicationState).IsEqualTo(ContentPublicationState.InReview);
        await Assert.That(viewModel.IsPublished).IsFalse();
    }
}
