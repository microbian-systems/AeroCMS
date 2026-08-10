using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Modules.Setup;
using Aero.Cms.Modules.Setup.Bootstrap;
using Aero.Cms.Modules.Setup.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Aero.Cms.Core.Tests.Integration;

public class SetupAuthenticationHandoffTests
{
    [Test]
    public async Task Handoff_accepts_remote_manager_provider_and_persists_requested_intent()
    {
        var database = Substitute.For<IDatabaseBootstrapService>();
        var service = CreateService(database);
        var request = CreateRequest(
            AuthenticationProviderSelections.Manager.EntraWorkforce,
            AuthenticationProviderSelections.Member.Disabled);

        var result = await service.CompleteAndHandoffAsync(request);

        await Assert.That(result.Succeeded).IsTrue();
        await database.Received(1).PersistAsync(
            Arg.Is<DatabaseBootstrapModel>(model =>
                model.RequestedManagerAuthenticationProvider == AuthenticationProviderSelections.Manager.EntraWorkforce &&
                model.RequestedMemberAuthenticationProvider == AuthenticationProviderSelections.Member.Disabled),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handoff_accepts_local_member_provider_and_persists_requested_intent()
    {
        var database = Substitute.For<IDatabaseBootstrapService>();
        var service = CreateService(database);
        var request = CreateRequest(
            AuthenticationProviderSelections.Manager.Local,
            AuthenticationProviderSelections.Member.Local);

        var result = await service.CompleteAndHandoffAsync(request);

        await Assert.That(result.Succeeded).IsTrue();
        await database.Received(1).PersistAsync(
            Arg.Is<DatabaseBootstrapModel>(model =>
                model.RequestedManagerAuthenticationProvider == AuthenticationProviderSelections.Manager.Local &&
                model.RequestedMemberAuthenticationProvider == AuthenticationProviderSelections.Member.Local),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handoff_persists_resolved_manager_and_member_providers()
    {
        var database = Substitute.For<IDatabaseBootstrapService>();
        var cache = Substitute.For<ICacheBootstrapService>();
        var pending = Substitute.For<IBootstrapPendingSetupRequestStore>();
        var completion = Substitute.For<IBootstrapCompletionWriter>();
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        var service = new SetupBootstrapHandoffService(
            database,
            cache,
            pending,
            completion,
            Substitute.For<IEnvironmentAppSettingsWriter>(),
            HostEnvironment(),
            lifetime,
            Substitute.For<ILogger<SetupBootstrapHandoffService>>());
        var request = CreateRequest(
            AuthenticationProviderSelections.Manager.Local,
            AuthenticationProviderSelections.Member.WorkOs);

        var result = await service.CompleteAndHandoffAsync(request);

        await Assert.That(result.Succeeded).IsTrue();
        await database.Received(1).PersistAsync(
            Arg.Is<DatabaseBootstrapModel>(model =>
                model.RequestedManagerAuthenticationProvider == AuthenticationProviderSelections.Manager.Local
                && model.RequestedMemberAuthenticationProvider == AuthenticationProviderSelections.Member.WorkOs),
            Arg.Any<CancellationToken>());
        lifetime.Received(1).StopApplication();
    }

    private static SetupBootstrapHandoffService CreateService(IDatabaseBootstrapService database)
        => new(
            database,
            Substitute.For<ICacheBootstrapService>(),
            Substitute.For<IBootstrapPendingSetupRequestStore>(),
            Substitute.For<IBootstrapCompletionWriter>(),
            Substitute.For<IEnvironmentAppSettingsWriter>(),
            HostEnvironment(),
            Substitute.For<IHostApplicationLifetime>(),
            Substitute.For<ILogger<SetupBootstrapHandoffService>>());

    private static IHostEnvironment HostEnvironment()
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns("Test");
        return environment;
    }

    private static SeedDatabaseRequest CreateRequest(string managerProvider, string memberProvider)
        => new(
            "Embedded",
            "Local",
            "Local Certificate",
            managerProvider,
            memberProvider,
            null,
            null,
            null,
            null,
            "admin",
            "admin@example.com",
            "correct horse battery",
            "Aero CMS",
            "Welcome",
            "Blog",
            "localhost",
            "en-US",
            ["en-US"]);
}
