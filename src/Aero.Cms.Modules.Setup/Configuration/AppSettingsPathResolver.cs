namespace Aero.Cms.Modules.Setup.Configuration;

/// <summary>
/// Resolves the web host's environment settings file from the setup application's output location.
/// </summary>
/// <remarks>
/// The default layout assumes both projects retain their repository-relative positions.
/// Callers may supply a base directory for deterministic tooling or tests.
/// </remarks>
public static class AppSettingsPathResolver
{
    /// <summary>
    /// Resolves the absolute path of the <c>Aero.Cms.Web</c> project directory.
    /// </summary>
    /// <param name="baseDirectory">The path from which to traverse, or <see langword="null"/> to use <see cref="AppContext.BaseDirectory"/>.</param>
    /// <returns>The normalized absolute web project path; the directory is not required to exist.</returns>
public static string GetWebProjectPath(string? baseDirectory = null)
        => Path.GetFullPath(baseDirectory ?? Directory.GetCurrentDirectory());

    /// <summary>
    /// Resolves the environment-specific settings file used by the web host.
    /// </summary>
    /// <param name="environmentName">The environment suffix to embed in the file name.</param>
    /// <param name="baseDirectory">The optional traversal base accepted by <see cref="GetWebProjectPath"/>.</param>
    /// <returns>The path to <c>appsettings.{environmentName}.json</c>.</returns>
    /// <exception cref="ArgumentException"><paramref name="environmentName"/> is <see langword="null"/>, empty, or whitespace.</exception>
public static string GetAppSettingsFilePath(string environmentName, string? baseDirectory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentName);

        var fileName = $"appsettings.{environmentName}.json";

        return Path.Combine(GetWebProjectPath(baseDirectory), fileName);
    }
}
