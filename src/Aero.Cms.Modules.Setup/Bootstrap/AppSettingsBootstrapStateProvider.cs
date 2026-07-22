using Microsoft.Extensions.Configuration;
using Aero.Cms.Abstractions.Authentication;

namespace Aero.Cms.Modules.Setup.Bootstrap;

/// <summary>
/// Reads the setup bootstrap state from the active application configuration.
/// </summary>
/// <remarks>
/// When an explicit state is absent, the provider derives a backward-compatible state
/// from the completion flags and the presence of a database mode. This type only reads
/// configuration; it does not persist or validate it.
/// </remarks>
public sealed class AppSettingsBootstrapStateProvider(IConfiguration configuration) : IBootstrapStateProvider
{
    /// <inheritdoc />
public BootstrapState GetState()
    {
        var section = configuration.GetSection("AeroCms:Bootstrap");
        var hasBootstrapConfig = section.GetValue<bool?>("HasBootstrapConfig")
            ?? (section.Exists() && !string.IsNullOrWhiteSpace(section["DatabaseMode"]));
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
            DatabaseMode = section["DatabaseMode"],
            CacheMode = section["CacheMode"],
            SecretProvider = section["SecretProvider"],
            RequestedManagerAuthenticationProvider = section["RequestedManagerAuthenticationProvider"]
                ?? AuthenticationProviderSelections.Manager.Local,
            RequestedMemberAuthenticationProvider = section["RequestedMemberAuthenticationProvider"]
                ?? AuthenticationProviderSelections.Member.Disabled
        };
    }
}
