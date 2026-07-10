using System.Text.RegularExpressions;
using Aero.Cms.Abstractions.Blocks.Embed;

namespace Aero.Cms.Ui.Neo.Embed;

/// <summary>
/// Resolves Vimeo URLs to the standard embed player.
/// </summary>
public sealed partial class VimeoEmbedResolver : IEmbedUrlResolver
{
    [GeneratedRegex(@"vimeo\.com/(?<id>\d+)", RegexOptions.Compiled)]
    private static partial Regex Pattern();

        /// <summary>
    /// CanResolve method.
    /// </summary>
public bool CanResolve(Uri uri) => uri.Host.Contains("vimeo.com");

        /// <summary>
    /// Resolve method.
    /// </summary>
public EmbedResolvedUrl Resolve(Uri uri)
    {
        var id = Pattern().Match(uri.ToString()).Groups["id"].Value;
        return new EmbedResolvedUrl(
            EmbedSrc: $"https://player.vimeo.com/video/{id}",
            DefaultRatio: AspectRatio.Widescreen,
            DefaultSandbox: SandboxFlags.Video);
    }
}
