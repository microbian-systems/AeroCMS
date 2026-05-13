using Aero.Cms.Modules.Navigation.Domain;
using Aero.Cms.Modules.Navigation.Events;
using FluentAssertions;

namespace Aero.Cms.Modules.Navigation.Tests;

public sealed class NavMenuDocumentTests
{
    [Test]
    public void Publish_MarksCurrentStateAsPublished()
    {
        var created = new NavMenuCreated(10, "Header", "header", null, DateTimeOffset.UtcNow);
        var menu = NavMenuDocument.Create(100, created);
        var snapshot = NavMenuSnapshot.Empty;

        menu.Apply(new NavMenuDraftSaved(10, "Header", "header", snapshot, null, DateTimeOffset.UtcNow, null));
        menu.Apply(new NavMenuPublished(10, snapshot, null, DateTimeOffset.UtcNow, null));

        menu.State.Should().Be(NavMenuLifecycleState.Published);
        menu.HasPublishedSnapshot.Should().BeTrue();
    }

    [Test]
    public void SaveDraftAfterPublish_MarksStateAsPublishedWithDraft()
    {
        var created = new NavMenuCreated(10, "Header", "header", null, DateTimeOffset.UtcNow);
        var menu = NavMenuDocument.Create(100, created);
        var snapshot = NavMenuSnapshot.Empty;

        menu.Apply(new NavMenuDraftSaved(10, "Header", "header", snapshot, null, DateTimeOffset.UtcNow, null));
        menu.Apply(new NavMenuPublished(10, snapshot, null, DateTimeOffset.UtcNow, null));
        menu.Apply(new NavMenuDraftSaved(10, "Header revised", "header-revised", snapshot, null, DateTimeOffset.UtcNow, null));

        menu.State.Should().Be(NavMenuLifecycleState.PublishedWithDraft);
        menu.HasPublishedSnapshot.Should().BeTrue();
    }
}
