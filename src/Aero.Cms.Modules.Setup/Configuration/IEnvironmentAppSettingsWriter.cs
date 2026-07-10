namespace Aero.Cms.Modules.Setup.Configuration;

/// <summary>
/// Defines an interface for IEnvironmentAppSettingsWriter.
/// </summary>
public interface IEnvironmentAppSettingsWriter
{
        /// <summary>
    /// WriteAsync method.
    /// </summary>
Task WriteAsync(string environmentName, string json, CancellationToken cancellationToken = default);
}
