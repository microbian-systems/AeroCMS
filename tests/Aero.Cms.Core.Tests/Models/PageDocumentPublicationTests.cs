using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Events;
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
