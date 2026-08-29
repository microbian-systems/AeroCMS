using Aero.Cms.Modules.Setup;
using AeroDB.Sable;
using NSubstitute;
using Shouldly;

namespace Aero.Cms.Core.Tests.Integration;

public sealed class ServerTargetSetupExecutorTests
{
    [Test]
    public async Task InitializeSchemaAndOpenSessionAsync_InitializesBeforeOpeningSession()
    {
        using var cancellation = new CancellationTokenSource();
        var store = Substitute.For<IDocumentStore>();
        var session = Substitute.For<IDocumentSession>();
        store.InitializeAsync(cancellation.Token).Returns(Task.CompletedTask);
        store.LightweightSessionAsync(cancellation.Token).Returns(Task.FromResult(session));

        var result = await ServerTargetSetupExecutor.InitializeSchemaAndOpenSessionAsync(
            store,
            cancellation.Token);

        result.ShouldBeSameAs(session);
        Received.InOrder(() =>
        {
            store.InitializeAsync(cancellation.Token);
            store.LightweightSessionAsync(cancellation.Token);
        });
    }

    [Test]
    public async Task InitializeSchemaAndOpenSessionAsync_DoesNotSeedWhenInitializationFails()
    {
        var store = Substitute.For<IDocumentStore>();
        store.InitializeAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("schema failed")));

        await Assert.That(async () =>
                await ServerTargetSetupExecutor.InitializeSchemaAndOpenSessionAsync(store, CancellationToken.None))
            .Throws<InvalidOperationException>();

        await store.DidNotReceiveWithAnyArgs().LightweightSessionAsync(default);
    }
}
