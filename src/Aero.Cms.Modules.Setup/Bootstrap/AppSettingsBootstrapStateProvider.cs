using Microsoft.Extensions.Configuration;

namespace Aero.Cms.Modules.Setup.Bootstrap;

public sealed class AppSettingsBootstrapStateProvider(IConfiguration configuration) : IBootstrapStateProvider
{
    public BootstrapState GetState()
    {
        var section = configuration.GetSection("AeroCms:Bootstrap");
        var setupComplete = section.GetValue<bool?>("SetupComplete") ?? false;
        var seedComplete = section.GetValue<bool?>("SeedComplete") ?? false;
        var state = section["State"];

        if (string.IsNullOrWhiteSpace(state))
        {
            state = setupComplete && seedComplete
                ? BootstrapStates.Running
                : section.Exists()
                    ? BootstrapStates.Configured
                    : BootstrapStates.Setup;
        }

        return new BootstrapState
        {
            HasBootstrapConfig = section.Exists(),
            State = state,
            SetupComplete = setupComplete,
            SeedComplete = seedComplete,
            DatabaseMode = section["DatabaseMode"],
            CacheMode = section["CacheMode"],
            SecretProvider = section["SecretProvider"]
        };
    }
}
