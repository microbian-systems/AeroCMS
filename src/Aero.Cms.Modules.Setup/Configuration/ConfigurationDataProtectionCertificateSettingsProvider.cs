using Microsoft.Extensions.Configuration;
using Aero.AppServer.Startup;

namespace Aero.Cms.Modules.Setup.Configuration;

/// <summary>
/// Adapts the host's data-protection bootstrap settings into the setup module contract.
/// </summary>
public sealed class ConfigurationDataProtectionCertificateSettingsProvider(IConfiguration configuration) : IDataProtectionCertificateSettingsProvider
{
    /// <inheritdoc />
public DataProtectionCertificateSettings GetSettings()
    {
        var settings = DataProtectionCertificateBootstrapper.ResolveSettings(configuration);

        return new DataProtectionCertificateSettings
        {
            CertificatePath = settings.CertificatePath,
            CertificatePassword = settings.CertificatePassword,
            KeyRingPath = settings.KeyRingPath
        };
    }
}
