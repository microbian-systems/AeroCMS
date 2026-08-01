using Aero.Cms.Modules.Footer.Events;
using Aero.Cms.Modules.Footer.Projections;
using AeroDB.Sable;
using Shouldly;

namespace Aero.Cms.Modules.Footer.Tests;

public sealed class ProjectionContractTests
{
    [Test]
    public void FooterProjection_DeclaresEveryHandledEventType()
    {
        IProjection projection = new FooterDocumentProjection();

        projection.EventTypes.ShouldBe([
            typeof(FooterCreated),
            typeof(FooterDraftSaved),
            typeof(FooterPublished),
            typeof(FooterArchived)
        ], ignoreOrder: true);
    }

    [Test]
    public void SiteSettingsProjection_DeclaresDefaultFooterEvent()
    {
        IProjection projection = new SiteFooterSettingsProjection();

        projection.EventTypes.ShouldBe([typeof(SiteDefaultFooterChanged)]);
    }
}
