using System.IO;
using System.Text;

namespace Aero.Cms.Modules.Setup.Configuration;

public sealed class EnvironmentAppSettingsWriter : IEnvironmentAppSettingsWriter
{
    private readonly string _webProjectPath;

    public EnvironmentAppSettingsWriter(string? webProjectPath = null)
    {
        _webProjectPath = webProjectPath ?? AppSettingsPathResolver.GetWebProjectPath();
    }

    public async Task WriteAsync(string environmentName, string json, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentName);
        ArgumentException.ThrowIfNullOrEmpty(json);

        var targetFile = AppSettingsPathResolver.GetAppSettingsFilePath(environmentName, _webProjectPath);

        var directory = Path.GetDirectoryName(targetFile) ?? _webProjectPath;
        Directory.CreateDirectory(directory);

        var tempFile = Path.Combine(directory, $".{Path.GetFileName(targetFile)}.{Guid.NewGuid():N}.tmp");
        await File.WriteAllTextAsync(tempFile, json, Encoding.UTF8, cancellationToken);

        if (File.Exists(targetFile))
        {
            File.Replace(tempFile, targetFile, null);
            return;
        }

        File.Move(tempFile, targetFile);
    }
}
