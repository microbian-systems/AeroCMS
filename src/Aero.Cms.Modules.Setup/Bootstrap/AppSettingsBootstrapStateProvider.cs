using Microsoft.Extensions.Configuration;
using Aero.Cms.Abstractions.Authentication;
using Aero.AppServer.Startup;

namespace Aero.Cms.Modules.Setup.Bootstrap;

/// <summary>
/// Reads the setup bootstrap state from the active application configuration.
/// </summary>
/// <remarks>
/// Lifecycle state is read only from <c>AeroCms:Bootstrap</c>. Infrastructure selections are
/// read independently from <c>AeroCms:Infrastructure</c>, so preconfiguring a database or cache
/// never skips the setup wizard by itself.
/// </remarks>
public sealed class AppSettingsBootstrapStateProvider(IConfiguration configuration) : IBootstrapStateProvider
{
    /// <inheritdoc />
    public BootstrapState GetState()
    {
        var section = configuration.GetSection("AeroCms:Bootstrap");
        var infrastructure = configuration.GetSection(AeroCmsInfrastructureConfiguration.SectionName);
        var hasBootstrapConfig = section.GetValue<bool?>("HasBootstrapConfig") ?? false;
        var setupComplete = section.GetValue<bool?>("SetupComplete") ?? false;
        var seedComplete = section.GetValue<bool?>("SeedComplete") ?? false;
        var state = section["State"];

        if (string.IsNullOrWhiteSpace(state))
        {
            state = setupComplete && seedComplete
                ? BootstrapStates.Running
                : hasBootstrapConfig
                    ? BootstrapStates.Configured
                    : BootstrapStates.Setup;
        }

        return new BootstrapState
        {
            HasBootstrapConfig = hasBootstrapConfig,
            State = state,
            SetupComplete = setupComplete,
            SeedComplete = seedComplete,
            DatabaseMode = infrastructure[AeroCmsInfrastructureConfiguration.DatabaseMode],
            CacheMode = infrastructure[AeroCmsInfrastructureConfiguration.CacheMode],
            SecretProvider = infrastructure[AeroCmsInfrastructureConfiguration.SecretProvider],
            RequestedManagerAuthenticationProvider = section["RequestedManagerAuthenticationProvider"]
                ?? AuthenticationProviderSelections.Manager.Local,
            RequestedMemberAuthenticationProvider = section["RequestedMemberAuthenticationProvider"]
                ?? AuthenticationProviderSelections.Member.Disabled
        };
    }
}
