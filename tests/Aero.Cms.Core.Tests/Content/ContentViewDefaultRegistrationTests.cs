using Aero.Cms.Abstractions.Content.Views;
using Aero.Cms.Core.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TUnit.Core;

namespace Aero.Cms.Core.Tests.Content;

public sealed class ContentViewDefaultRegistrationTests
{
    [Test]
    public void Relationship_plan_services_are_explicitly_fail_closed_by_default()
    {
        var services = new ServiceCollection();
        services.AddContentTypeSystem();
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IContentViewTrustedQueryPlanRegistry>()
            .ShouldBeOfType<EmptyContentViewTrustedQueryPlanRegistry>();
        provider.GetRequiredService<IContentViewRelationshipPlanDialectCapability>()
            .ShouldBeOfType<DisabledContentViewRelationshipPlanDialectCapability>();
    }
}
