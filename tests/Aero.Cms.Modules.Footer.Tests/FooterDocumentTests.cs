using Aero.Cms.Modules.Footer.Domain;
using Aero.Cms.Modules.Footer.Events;
using FluentAssertions;

namespace Aero.Cms.Modules.Footer.Tests;

public sealed class FooterDocumentTests
{
    [Test]
    public void Publish_MarksCurrentStateAsPublished()
    {
        var created = new FooterCreated(10, "Footer", "footer", null, null, DateTimeOffset.UtcNow);
        var footer = FooterDocument.Create(100, created);
        var snapshot = FooterSnapshot.Empty;

        footer.Apply(new FooterDraftSaved(10, "Footer", "footer", null, snapshot, null, DateTimeOffset.UtcNow, null));
        footer.Apply(new FooterPublished(10, snapshot, null, DateTimeOffset.UtcNow, null));

        footer.State.Should().Be(FooterLifecycleState.Published);
        footer.HasPublishedSnapshot.Should().BeTrue();
    }

    [Test]
    public void SaveDraftAfterPublish_MarksStateAsPublishedWithDraft()
    {
        var created = new FooterCreated(10, "Footer", "footer", null, null, DateTimeOffset.UtcNow);
        var footer = FooterDocument.Create(100, created);
        var snapshot = FooterSnapshot.Empty;

        footer.Apply(new FooterDraftSaved(10, "Footer", "footer", null, snapshot, null, DateTimeOffset.UtcNow, null));
        footer.Apply(new FooterPublished(10, snapshot, null, DateTimeOffset.UtcNow, null));
        footer.Apply(new FooterDraftSaved(10, "Footer revised", "footer", null, snapshot, null, DateTimeOffset.UtcNow, null));

        footer.State.Should().Be(FooterLifecycleState.PublishedWithDraft);
        footer.HasPublishedSnapshot.Should().BeTrue();
    }
}
