namespace Aero.Cms.Modules.Setup.Configuration;

/// <summary>
/// Defines an interface for IDataProtectionCertificateSettingsProvider.
/// </summary>
public interface IDataProtectionCertificateSettingsProvider
{
        /// <summary>
    /// GetSettings method.
    /// </summary>
DataProtectionCertificateSettings GetSettings();
}
