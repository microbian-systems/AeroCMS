using System.Reflection;
using Aero.Cms.Modules.Ai.Knowledge;
using Shouldly;
using Wolverine.Attributes;
using Wolverine.Configuration;
using Wolverine.Transports.Local;

namespace Aero.Cms.Core.Tests.Ai;

public sealed class AeroAiKnowledgeProjectionHandlerConfigurationTests
{
    [Test]
    public void Projection_handler_uses_an_isolated_sequential_local_queue()
    {
        var handlerType = typeof(AeroAiKnowledgeProjectionHandler);
        var stickyHandler = handlerType.GetCustomAttribute<StickyHandlerAttribute>();

        stickyHandler.ShouldNotBeNull();
        stickyHandler.EndpointName.ShouldBe("aero-ai-knowledge-projections");
        typeof(IConfigureLocalQueue).IsAssignableFrom(handlerType).ShouldBeTrue();

        var endpoint = new LocalQueue(stickyHandler.EndpointName);
        var configuration = new LocalQueueConfiguration(endpoint);

        AeroAiKnowledgeProjectionHandler.Configure(configuration);
        ((IDelayedEndpointConfiguration)configuration).Apply();

        endpoint.MaxDegreeOfParallelism.ShouldBe(1);
    }
}
