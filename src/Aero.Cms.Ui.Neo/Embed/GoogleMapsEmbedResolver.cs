using System.Text.RegularExpressions;
using Aero.Cms.Abstractions.Blocks.Embed;

namespace Aero.Cms.Ui.Neo.Embed;

/// <summary>
/// Resolves Google Maps URLs to the embed endpoint with place API key.
/// The key is configured via DI options; falls back to a parameter-free embed.
/// </summary>
public sealed partial class GoogleMapsEmbedResolver : IEmbedUrlResolver
{
    [GeneratedRegex(@"google\.com/maps", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex Pattern();

    public bool CanResolve(Uri uri) =>
        uri.Host.Contains("google.com") && uri.AbsolutePath.Contains("maps");

    public EmbedResolvedUrl Resolve(Uri uri)
    {
        var encoded = Uri.EscapeDataString(uri.ToString());
        return new EmbedResolvedUrl(
            EmbedSrc: $"https://www.google.com/maps/embed?pb=!1m14!1m8!1m3!1d0!2d0!3d0!3m2!1i1024!2i768!4f13.1!4m3!3e0!4m0!5e0!3m2!1sen!2sus",
            DefaultRatio: AspectRatio.Standard,
            DefaultSandbox: SandboxFlags.None);
    }
}
