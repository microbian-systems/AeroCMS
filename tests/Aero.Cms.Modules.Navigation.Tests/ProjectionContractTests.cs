using Aero.Cms.Modules.Navigation.Events;
using Aero.Cms.Modules.Navigation.Projections;
using AeroDB.Sable;
using FluentAssertions;

namespace Aero.Cms.Modules.Navigation.Tests;

public sealed class ProjectionContractTests
{
    [Test]
    public void NavMenuProjection_DeclaresEveryHandledEventType()
    {
        IProjection projection = new NavMenuDocumentProjection();

        projection.EventTypes.Should().BeEquivalentTo([
            typeof(NavMenuCreated),
            typeof(NavMenuDraftSaved),
            typeof(NavMenuPublished),
            typeof(NavMenuArchived)
        ]);
    }

    [Test]
    public void SiteSettingsProjection_DeclaresDefaultMenuEvent()
    {
        IProjection projection = new SiteNavigationSettingsProjection();

        projection.EventTypes.Should().Equal(typeof(SiteDefaultNavMenuChanged));
    }
}
