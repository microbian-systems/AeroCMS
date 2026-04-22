using System.IO;

namespace Aero.Cms.Modules.Setup.Configuration;

public static class AppSettingsPathResolver
{
    public static string GetWebProjectPath(string? baseDirectory = null)
        => Path.GetFullPath(Path.Combine(baseDirectory ?? AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Aero.Cms.Web"));

    public static string GetAppSettingsFilePath(string environmentName, string? baseDirectory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentName);

        var fileName = $"appsettings.{environmentName}.json";

        return Path.Combine(GetWebProjectPath(baseDirectory), fileName);
    }
}
