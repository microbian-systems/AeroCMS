namespace Aero.Cms.Modules.Setup.Configuration;

/// <summary>
/// Contains the certificate and key-ring locations used to bootstrap ASP.NET Core data protection.
/// </summary>
/// <remarks>
/// <see cref="CertificatePassword"/> is sensitive and must not be logged or exposed to clients.
/// The settings object does not load or validate the certificate.
/// </remarks>
public sealed record DataProtectionCertificateSettings
{
    /// <summary>
    /// Gets the configured certificate file path.
    /// </summary>
public string? CertificatePath { get; init; }

    /// <summary>
    /// Gets the password used to open the certificate file.
    /// </summary>
public string? CertificatePassword { get; init; }

    /// <summary>
    /// Gets the directory in which the persistent data-protection key ring is stored.
    /// </summary>
public string? KeyRingPath { get; init; }

    /// <summary>
    /// Gets whether a non-blank certificate path is configured.
    /// </summary>
public bool HasValue => !string.IsNullOrWhiteSpace(CertificatePath);
}
