using Microsoft.Extensions.Configuration;

namespace Aero.Cms.Modules.Setup.Configuration;

public sealed class ConfigurationDataProtectionCertificateSettingsProvider(IConfiguration configuration) : IDataProtectionCertificateSettingsProvider
{
    public DataProtectionCertificateSettings GetSettings()
    {
        static string? GetEnv(string name)
            => Environment.GetEnvironmentVariable(name)
               ?? Environment.GetEnvironmentVariable(name.Replace(":", "__"));

        var path = GetEnv("DataProtection:CertificatePath")
            ?? configuration["DataProtection:CertificatePath"];
        var password = GetEnv("DataProtection:CertificatePassword")
            ?? configuration["DataProtection:CertificatePassword"];

        return new DataProtectionCertificateSettings
        {
            CertificatePath = path,
            CertificatePassword = password
        };
    }
}
