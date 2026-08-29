using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace Aero.Cms.Modules.Setup.Bootstrap;

/// <summary>
/// Copies immutable Aero CMS starter media from the UI RCL into the host-owned media root.
/// </summary>
internal static class StarterMediaStager
{
    internal const string SourceSubpath = "_content/Aero.Cms.UI/media";

    /// <summary>
    /// Resolves the host-owned media directory even when the consuming app has no physical web root yet.
    /// </summary>
    internal static string ResolveHostMediaRoot(IWebHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        var webRootPath = environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRootPath))
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(environment.ContentRootPath);
            webRootPath = Path.Combine(environment.ContentRootPath, "wwwroot");
        }

        return Path.GetFullPath(Path.Combine(webRootPath, "media"));
    }

    /// <summary>
    /// Stages missing starter files without overwriting media already owned by the host.
    /// </summary>
    /// <returns><see langword="true"/> when the RCL media directory was available.</returns>
    internal static async Task<bool> StageAsync(
        IWebHostEnvironment environment,
        CancellationToken cancellationToken = default)
        => await StageAsync(environment, static entry => entry.CreateReadStream(), cancellationToken);

    internal static async Task<bool> StageAsync(
        IWebHostEnvironment environment,
        Func<IFileInfo, Stream> openReadStream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(openReadStream);

        var sourceProvider = environment.WebRootFileProvider;
        if (sourceProvider is null || !sourceProvider.GetDirectoryContents(SourceSubpath).Exists)
        {
            return false;
        }

        var targetRoot = ResolveHostMediaRoot(environment);
        Directory.CreateDirectory(targetRoot);
        await CopyDirectoryAsync(
            sourceProvider,
            SourceSubpath,
            targetRoot,
            targetRoot,
            openReadStream,
            cancellationToken);
        return true;
    }

    private static async Task CopyDirectoryAsync(
        IFileProvider sourceProvider,
        string sourceSubpath,
        string targetDirectory,
        string targetRoot,
        Func<IFileInfo, Stream> openReadStream,
        CancellationToken cancellationToken)
    {
        foreach (var entry in sourceProvider.GetDirectoryContents(sourceSubpath))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (entry.Name != Path.GetFileName(entry.Name))
            {
                throw new InvalidOperationException($"Starter media entry '{entry.Name}' is not a contained file name.");
            }

            var targetPath = Path.GetFullPath(Path.Combine(targetDirectory, entry.Name));
            if (!targetPath.StartsWith(targetRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Starter media entry '{entry.Name}' escaped the host media root.");
            }

            if (entry.IsDirectory)
            {
                Directory.CreateDirectory(targetPath);
                await CopyDirectoryAsync(
                    sourceProvider,
                    $"{sourceSubpath}/{entry.Name}",
                    targetPath,
                    targetRoot,
                    openReadStream,
                    cancellationToken);
                continue;
            }

            if (File.Exists(targetPath))
            {
                continue;
            }

            var temporaryPath = $"{targetPath}.aerocms-staging-{Guid.NewGuid():N}.tmp";
            try
            {
                await using (var source = openReadStream(entry))
                await using (var target = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 81920,
                    useAsync: true))
                {
                    await source.CopyToAsync(target, cancellationToken);
                    await target.FlushAsync(cancellationToken);
                }

                try
                {
                    File.Move(temporaryPath, targetPath, overwrite: false);
                }
                catch (IOException) when (File.Exists(targetPath))
                {
                    // Another setup attempt completed the same file first. Preserve that host-owned winner.
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }
    }
}
