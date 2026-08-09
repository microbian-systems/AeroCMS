using System.Reflection;
using Aero.Cms.Abstractions.Events;
using Aero.Cms.Modules.Ai.Knowledge;
using AeroDB.Sable;
using Shouldly;
using Wolverine;
using Wolverine.Attributes;
using Wolverine.Configuration;
using Wolverine.ErrorHandling;
using Wolverine.Runtime.Handlers;
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

    [Test]
    public void Projection_handler_retries_only_typed_transaction_conflicts_then_dead_letters()
    {
        var chain = new HandlerChain(
            typeof(PageContentUpdatedEvent),
            new HandlerGraph());

        AeroAiKnowledgeProjectionHandler.Configure(chain);

        var rule = chain.Failures.ShouldHaveSingleItem();
        rule.Count().ShouldBe(3);

        var transactionConflict = new TransactionConflictException(
            "Transaction conflict",
            new InvalidOperationException("provider conflict"));
        var firstAttempt = new Envelope { Attempts = 1 };

        rule.TryCreateContinuation(
                transactionConflict,
                firstAttempt,
                out var scheduledRetry)
            .ShouldBeTrue();
        scheduledRetry.ShouldBeAssignableTo<IContinuationSource>()
            .Description.ShouldContain("Schedule Retry");

        rule.TryCreateContinuation(
                new InvalidOperationException("permanent failure"),
                new Envelope { Attempts = 1 },
                out _)
            .ShouldBeFalse();

        rule.TryCreateContinuation(
                transactionConflict,
                new Envelope { Attempts = 4 },
                out var exhausted)
            .ShouldBeTrue();
        exhausted.ToString().ShouldBe("Move to Error Queue");
    }
}
