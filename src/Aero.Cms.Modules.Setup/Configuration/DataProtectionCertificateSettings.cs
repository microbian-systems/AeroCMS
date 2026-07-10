namespace Aero.Cms.Modules.Setup.Configuration;

/// <summary>
/// Represents a record for DataProtectionCertificateSettings.
/// </summary>
public sealed record DataProtectionCertificateSettings
{
        /// <summary>
    /// Gets or sets the Certificate Path.
    /// </summary>
public string? CertificatePath { get; init; }

        /// <summary>
    /// Gets or sets the Certificate Password.
    /// </summary>
public string? CertificatePassword { get; init; }

        /// <summary>
    /// Gets or sets the Key Ring Path.
    /// </summary>
public string? KeyRingPath { get; init; }

        /// <summary>
    /// Gets or sets the Has Value.
    /// </summary>
public bool HasValue => !string.IsNullOrWhiteSpace(CertificatePath);
}
