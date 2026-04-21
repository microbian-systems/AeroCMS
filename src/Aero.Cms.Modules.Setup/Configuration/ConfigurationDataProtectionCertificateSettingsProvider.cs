using Microsoft.Extensions.Configuration;
using Aero.AppServer.Startup;

namespace Aero.Cms.Modules.Setup.Configuration;

public sealed class ConfigurationDataProtectionCertificateSettingsProvider(IConfiguration configuration) : IDataProtectionCertificateSettingsProvider
{
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
