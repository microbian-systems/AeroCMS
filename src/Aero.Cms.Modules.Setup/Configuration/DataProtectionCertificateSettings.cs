namespace Aero.Cms.Modules.Setup.Configuration;

public sealed record DataProtectionCertificateSettings
{
    public string? CertificatePath { get; init; }

    public string? CertificatePassword { get; init; }

    public bool HasValue => !string.IsNullOrWhiteSpace(CertificatePath);
}
