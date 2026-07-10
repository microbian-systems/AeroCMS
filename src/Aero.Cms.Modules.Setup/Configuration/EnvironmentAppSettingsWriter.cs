namespace Aero.Cms.Modules.Setup.Configuration;

/// <summary>
/// Represents a class for EnvironmentAppSettingsWriter.
/// </summary>
public sealed class EnvironmentAppSettingsWriter : IEnvironmentAppSettingsWriter
{
    private readonly string _webProjectPath;

        /// <summary>
    /// Initializes a new instance of the <see cref="EnvironmentAppSettingsWriter"/> class.
    /// </summary>
public EnvironmentAppSettingsWriter(string? webProjectPath = null)
    {
        _webProjectPath = webProjectPath ?? AppSettingsPathResolver.GetWebProjectPath();
    }

        /// <summary>
    /// WriteAsync method.
    /// </summary>
public async Task WriteAsync(string environmentName, string json, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentName);
        ArgumentException.ThrowIfNullOrEmpty(json);

        var targetFile = Path.Combine(_webProjectPath, $"appsettings.{environmentName}.json");

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
