using Aero.AppServer.Startup;
using Aero.Cms.Modules.Setup;
using Aero.Cms.Modules.Setup.Bootstrap;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Aero.Cms.Core.Tests.Integration;

public sealed class BootstrapDatabaseScopeGuardTests
{
    [Test]
    public async Task Pending_setup_scope_must_match_the_configured_runtime_scope()
    {
        var request = CreateRequest("example_namespace", "example_cms");
        var settings = CreateSettings("example_namespace", "other_database");

        var error = BootstrapDatabaseScopeGuard.GetValidationError(request, settings);

        await Assert.That(error).Contains("does not match");
        await Assert.That(error).Contains("example_namespace/example_cms");
        await Assert.That(error).Contains("example_namespace/other_database");
    }

    [Test]
    public async Task Matching_pending_and_runtime_scopes_are_accepted()
    {
        var request = CreateRequest(" example_namespace ", " example_cms ");
        var settings = CreateSettings("example_namespace", "example_cms");

        var error = BootstrapDatabaseScopeGuard.GetValidationError(request, settings);

        await Assert.That(error).IsNull();
    }

    [Test]
    public async Task Configured_runtime_without_a_pending_payload_fails_closed()
    {
        var initialization = Substitute.For<ISetupInitializationService>();
        initialization.GetBootstrapState().Returns(new BootstrapState
        {
            State = BootstrapStates.Configured,
            HasBootstrapConfig = true
        });
        var pending = Substitute.For<IBootstrapPendingSetupRequestStore>();
        pending.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SeedDatabaseRequest?>(null));
        var initializer = new RuntimeBootstrapInitializer(
            initialization,
            pending,
            Substitute.For<ISetupCompletionService>(),
            CreateSettings("aero", "aero"),
            Substitute.For<ILogger<RuntimeBootstrapInitializer>>());

        await Assert.That(async () => await initializer.InitializeAsync())
            .Throws<InvalidOperationException>();
    }

    private static SeedDatabaseRequest CreateRequest(string databaseNamespace, string databaseName) =>
        new(
            "Server",
            "Local",
            "Local Certificate",
            "Local",
            "Disabled",
            "ws://localhost:8000",
            null,
            null,
            null,
            "admin",
            "admin@example.test",
            "Password1!",
            "Example",
            "Welcome",
            "Blog",
            "example.test",
            "en-US",
            ["en-US"])
        {
            DatabaseNamespace = databaseNamespace,
            DatabaseName = databaseName
        };

    private static ResolvedInfrastructureSettings CreateSettings(string databaseNamespace, string databaseName) =>
        new("ws://localhost:8000", null, "Server", "Local", "Local Certificate")
        {
            DatabaseNamespace = databaseNamespace,
            DatabaseName = databaseName
        };
}
