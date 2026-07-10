using System.Text.RegularExpressions;
using Aero.Cms.Abstractions.Blocks.Embed;

namespace Aero.Cms.Ui.Neo.Embed;

/// <summary>
/// Resolves YouTube URLs (watch and short links) to the privacy-enhanced
/// youtube-nocookie.com embed player.
/// </summary>
public sealed partial class YouTubeEmbedResolver : IEmbedUrlResolver
{
    [GeneratedRegex(@"(?:youtube\.com/watch\?v=|youtu\.be/)(?<id>[\w-]{11})", RegexOptions.Compiled)]
    private static partial Regex Pattern();

        /// <summary>
    /// CanResolve method.
    /// </summary>
public bool CanResolve(Uri uri) =>
        uri.Host.Contains("youtube.com") || uri.Host.Contains("youtu.be");

        /// <summary>
    /// Resolve method.
    /// </summary>
public EmbedResolvedUrl Resolve(Uri uri)
    {
        var id = Pattern().Match(uri.ToString()).Groups["id"].Value;
        return new EmbedResolvedUrl(
            EmbedSrc: $"https://www.youtube-nocookie.com/embed/{id}",
            DefaultRatio: AspectRatio.Widescreen,
            DefaultSandbox: SandboxFlags.Video);
    }
}
