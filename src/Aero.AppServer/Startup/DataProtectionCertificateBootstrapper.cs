using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Aero.Secrets;
using Microsoft.Extensions.Configuration;

namespace Aero.AppServer.Startup;

public sealed record DataProtectionBootstrapSettings(
    string CertificatePath,
    string KeyRingPath,
    string? CertificatePassword,
    string ApplicationName,
    string ProtectorPurpose);

public static class DataProtectionCertificateBootstrapper
{
    public const string DefaultApplicationName = "AeroCMS";
    public const string DefaultProtectorPurpose = "Aero.Secrets.V1";

    public static DataProtectionBootstrapSettings ResolveSettings(IConfiguration? configuration)
    {
        static string? Get(IConfiguration? cfg, params string[] keys)
        {
            foreach (var key in keys)
            {
                var envValue = Environment.GetEnvironmentVariable(key)
                    ?? Environment.GetEnvironmentVariable(key.Replace(":", "__"));

                if (!string.IsNullOrWhiteSpace(envValue))
                {
                    return envValue;
                }

                var configValue = cfg?[key];
                if (!string.IsNullOrWhiteSpace(configValue))
                {
                    return configValue;
                }
            }

            return null;
        }

        var contentRoot = Directory.GetCurrentDirectory();
        var secretsRoot = Path.Combine(contentRoot, ".aero");
        var certPath = Get(configuration, "DataProtection:CertificatePath")
            ?? Path.Combine(secretsRoot, "aero.pfx");
        var keyRingPath = Get(configuration, "DataProtection:KeyRingPath")
            ?? Path.Combine(secretsRoot, "keys");
        var certificatePassword = Get(configuration, "AERO_CERT_PASSWORD", "DataProtection:CertificatePassword");
        var applicationName = Get(configuration, "DataProtection:ApplicationName")
            ?? DefaultApplicationName;
        var protectorPurpose = Get(configuration, "DataProtection:ProtectorPurpose")
            ?? DefaultProtectorPurpose;

        return new DataProtectionBootstrapSettings(certPath, keyRingPath, certificatePassword, applicationName, protectorPurpose);
    }

    public static ISecretManager CreateSecretManager(IConfiguration? configuration)
    {
        var settings = ResolveSettings(configuration);
        var certificate = GetOrCreateCertificate(settings);
        return new DataProtectionCertificateSecretManager(certificate, settings.ApplicationName, settings.KeyRingPath, settings.ProtectorPurpose);
    }

    public static X509Certificate2 GetOrCreateCertificate(DataProtectionBootstrapSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Directory.CreateDirectory(Path.GetDirectoryName(settings.CertificatePath) ?? Directory.GetCurrentDirectory());
        Directory.CreateDirectory(settings.KeyRingPath);

        var password = EnsureCertificatePassword(settings);
        if (File.Exists(settings.CertificatePath))
        {
            return X509CertificateLoader.LoadPkcs12FromFile(settings.CertificatePath, password, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.DefaultKeySet);
        }

        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=Aero CMS Data Protection", ecdsa, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.DigitalSignature, false));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(10));
        File.WriteAllBytes(settings.CertificatePath, certificate.Export(X509ContentType.Pfx, password));

        return X509CertificateLoader.LoadPkcs12FromFile(settings.CertificatePath, password, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.DefaultKeySet);
    }

    private static string EnsureCertificatePassword(DataProtectionBootstrapSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.CertificatePassword))
        {
            return settings.CertificatePassword;
        }

        var passwordPath = Path.ChangeExtension(settings.CertificatePath, ".key");
        if (File.Exists(passwordPath))
        {
            return File.ReadAllText(passwordPath).Trim();
        }

        var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        File.WriteAllText(passwordPath, password);
        return password;
    }
}
