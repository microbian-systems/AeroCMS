namespace Aero.Cms.Modules.Setup.Configuration;

/// <summary>
/// Represents a class for AppSettingsPathResolver.
/// </summary>
public static class AppSettingsPathResolver
{
        /// <summary>
    /// GetWebProjectPath method.
    /// </summary>
public static string GetWebProjectPath(string? baseDirectory = null)
        => Path.GetFullPath(Path.Combine(baseDirectory ?? AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Aero.Cms.Web"));

        /// <summary>
    /// GetAppSettingsFilePath method.
    /// </summary>
public static string GetAppSettingsFilePath(string environmentName, string? baseDirectory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentName);

        var fileName = $"appsettings.{environmentName}.json";

        return Path.Combine(GetWebProjectPath(baseDirectory), fileName);
    }
}
