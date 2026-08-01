namespace Aero.Cms.Modules.Setup.Configuration;

/// <summary>
/// Writes a complete environment-specific application settings document.
/// </summary>
public interface IEnvironmentAppSettingsWriter
{
    /// <summary>
    /// Replaces the target environment settings file with the supplied JSON.
    /// </summary>
    /// <param name="environmentName">The environment suffix used in the settings file name.</param>
    /// <param name="json">The complete JSON document to persist.</param>
    /// <param name="cancellationToken">Cancels writing the temporary file.</param>
    /// <exception cref="ArgumentException"><paramref name="environmentName"/> is blank, or <paramref name="json"/> is <see langword="null"/> or empty.</exception>
Task WriteAsync(string environmentName, string json, CancellationToken cancellationToken = default);
}
