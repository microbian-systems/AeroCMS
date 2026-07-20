using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Aero.Secrets;
using Microsoft.Extensions.Configuration;

namespace Aero.AppServer.Startup;

/// <summary>
/// Describes the certificate and ASP.NET Core Data Protection key-ring settings used by local secrets.
/// </summary>
/// <param name="CertificatePath">The PFX file to load or create.</param>
/// <param name="KeyRingPath">The directory in which Data Protection persists its key ring.</param>
/// <param name="CertificatePassword">
/// The configured PFX password, or a null or whitespace value to load or create a sidecar password file.
/// </param>
/// <param name="ApplicationName">The Data Protection application discriminator.</param>
/// <param name="ProtectorPurpose">The purpose string used to isolate protected secret payloads.</param>
public sealed record DataProtectionBootstrapSettings(
    string CertificatePath,
    string KeyRingPath,
    string? CertificatePassword,
    string ApplicationName,
    string ProtectorPurpose);

/// <summary>
/// Resolves and provisions the certificate material used by the local-certificate secret provider.
/// </summary>
public static class DataProtectionCertificateBootstrapper
{
    /// <summary>
    /// Gets the default Data Protection application discriminator.
    /// </summary>
public const string DefaultApplicationName = "AeroCMS";
    /// <summary>
    /// Gets the default purpose string used to isolate Aero secret payloads.
    /// </summary>
public const string DefaultProtectorPurpose = "Aero.Secrets.V1";

    /// <summary>
    /// Resolves bootstrap paths and protection identifiers from environment variables and configuration.
    /// </summary>
    /// <param name="configuration">The optional configuration source.</param>
    /// <returns>The resolved settings, including defaults rooted at the current working directory.</returns>
    /// <remarks>
    /// Environment variables take precedence over configuration values. Both colon-delimited and
    /// double-underscore environment-variable forms are recognized.
    /// </remarks>
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
        var certPath = Get(configuration,
                "AeroCms:DataProtection:Certificate:Path",
                "DataProtection:CertificatePath")
            ?? Path.Combine(secretsRoot, "aero.pfx");
        var keyRingPath = Get(configuration,
                "AeroCms:DataProtection:KeyStoragePath",
                "DataProtection:KeyRingPath")
            ?? Path.Combine(secretsRoot, "keys");
        var certificatePassword = Get(configuration,
            "AERO_CERT_PASSWORD",
            "AeroCms:DataProtection:Certificate:Password",
            "DataProtection:CertificatePassword");
        var applicationName = Get(configuration,
                "AeroCms:DataProtection:ApplicationName",
                "DataProtection:ApplicationName")
            ?? DefaultApplicationName;
        var protectorPurpose = Get(configuration,
                "AeroCms:DataProtection:ProtectorPurpose",
                "DataProtection:ProtectorPurpose")
            ?? DefaultProtectorPurpose;

        return new DataProtectionBootstrapSettings(certPath, keyRingPath, certificatePassword, applicationName, protectorPurpose);
    }

    /// <summary>
    /// Creates a certificate-backed secret manager from the resolved bootstrap settings.
    /// </summary>
    /// <param name="configuration">The optional configuration source.</param>
    /// <returns>A secret manager configured with the provisioned certificate and key ring.</returns>
    /// <remarks>
    /// This operation may create directories, a PFX certificate, and a password sidecar file.
    /// </remarks>
public static ISecretManager CreateSecretManager(IConfiguration? configuration)
    {
        var settings = ResolveSettings(configuration);
        var certificate = GetOrCreateCertificate(settings);
        return new DataProtectionCertificateSecretManager(certificate, settings.ApplicationName, settings.KeyRingPath, settings.ProtectorPurpose);
    }

    /// <summary>
    /// Loads a compatible PFX certificate or replaces it with a new self-signed RSA certificate.
    /// </summary>
    /// <param name="settings">The certificate and key-ring settings.</param>
    /// <returns>An exportable certificate containing both RSA public and private keys.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="settings"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// An existing certificate without an RSA public/private key pair is moved to a timestamped
    /// <c>.unsupported-*</c> backup before replacement. File-system, cryptographic, password, and
    /// certificate-loading errors propagate to the caller.
    /// </remarks>
public static X509Certificate2 GetOrCreateCertificate(DataProtectionBootstrapSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Directory.CreateDirectory(Path.GetDirectoryName(settings.CertificatePath) ?? Directory.GetCurrentDirectory());
        Directory.CreateDirectory(settings.KeyRingPath);

        var password = EnsureCertificatePassword(settings);
        if (File.Exists(settings.CertificatePath))
        {
            var existing = X509CertificateLoader.LoadPkcs12FromFile(settings.CertificatePath, password, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.DefaultKeySet);
            if (IsSupportedForDataProtection(existing))
            {
                return existing;
            }

            existing.Dispose();
            BackupIncompatibleCertificate(settings.CertificatePath);
        }

        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=Aero CMS Data Protection", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.DigitalSignature, false));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(10));
        File.WriteAllBytes(settings.CertificatePath, certificate.Export(X509ContentType.Pfx, password));

        return X509CertificateLoader.LoadPkcs12FromFile(settings.CertificatePath, password, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.DefaultKeySet);
    }

    /// <summary>
    /// Determines whether a certificate has the RSA key pair required by Data Protection.
    /// </summary>
    /// <param name="certificate">The certificate to inspect.</param>
    /// <returns><see langword="true"/> when both RSA public and private keys are available.</returns>
    private static bool IsSupportedForDataProtection(X509Certificate2 certificate)
        => certificate.GetRSAPublicKey() is not null && certificate.GetRSAPrivateKey() is not null;

    /// <summary>
    /// Moves an incompatible PFX to a timestamped backup in the same directory.
    /// </summary>
    /// <param name="certificatePath">The path of the incompatible certificate.</param>
    private static void BackupIncompatibleCertificate(string certificatePath)
    {
        var backupPath = Path.Combine(
            Path.GetDirectoryName(certificatePath) ?? Directory.GetCurrentDirectory(),
            $"{Path.GetFileNameWithoutExtension(certificatePath)}.unsupported-{DateTime.UtcNow:yyyyMMddHHmmss}{Path.GetExtension(certificatePath)}");

        File.Move(certificatePath, backupPath, overwrite: true);
    }

    /// <summary>
    /// Resolves the configured certificate password or manages its generated sidecar value.
    /// </summary>
    /// <param name="settings">The certificate bootstrap settings.</param>
    /// <returns>The password used to load or export the PFX.</returns>
    /// <remarks>
    /// When no password is configured, a <c>.key</c> file next to the certificate is read. If it
    /// does not exist, a random 32-byte value is generated, Base64-encoded, and written there.
    /// File permissions are not hardened by this method; deployment must protect both files.
    /// </remarks>
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
