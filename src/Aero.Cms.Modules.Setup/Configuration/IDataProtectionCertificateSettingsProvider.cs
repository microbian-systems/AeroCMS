namespace Aero.Cms.Modules.Setup.Configuration;

/// <summary>
/// Provides certificate settings needed to establish persistent data-protection keys during setup.
/// </summary>
public interface IDataProtectionCertificateSettingsProvider
{
    /// <summary>
    /// Resolves the current certificate and key-ring settings.
    /// </summary>
    /// <returns>A settings snapshot; unset values are represented by <see langword="null"/>.</returns>
DataProtectionCertificateSettings GetSettings();
}
