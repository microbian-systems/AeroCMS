namespace Aero.Cms.Modules.Setup.Configuration;

/// <summary>
/// Atomically replaces an environment-specific settings file in the web project.
/// </summary>
/// <remarks>
/// Content is first written as UTF-8 to a temporary file in the target directory. An
/// existing target is replaced with <see cref="File.Replace(string, string, string?)"/>;
/// replacement failures propagate rather than falling back to a move. When the target does
/// not exist, the temporary file is moved into place. I/O and cancellation failures propagate
/// to the caller.
/// </remarks>
public sealed class EnvironmentAppSettingsWriter : IEnvironmentAppSettingsWriter
{
    private readonly string _webProjectPath;

    /// <summary>
    /// Initializes a writer for the specified web project directory.
    /// </summary>
    /// <param name="webProjectPath">The target project directory, or <see langword="null"/> to resolve the repository-relative default.</param>
public EnvironmentAppSettingsWriter(string webProjectPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(webProjectPath);
        _webProjectPath = Path.GetFullPath(webProjectPath);
    }

    /// <inheritdoc />
    public string GetFilePath(string environmentName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentName);
        return Path.Combine(_webProjectPath, $"appsettings.{environmentName}.json");
    }

    /// <inheritdoc />
public async Task WriteAsync(string environmentName, string json, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentName);
        ArgumentException.ThrowIfNullOrEmpty(json);

        var targetFile = GetFilePath(environmentName);

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
