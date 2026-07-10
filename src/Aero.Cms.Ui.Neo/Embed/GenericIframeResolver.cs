using Aero.Cms.Abstractions.Blocks.Embed;

namespace Aero.Cms.Ui.Neo.Embed;

/// <summary>
/// Fallback resolver for any HTTPS URL not handled by a specific provider.
/// Applies the most restrictive sandbox preset (Strict = no permissions).
/// </summary>
public sealed class GenericIframeResolver : IEmbedUrlResolver
{
        /// <summary>
    /// CanResolve method.
    /// </summary>
public bool CanResolve(Uri uri) => uri.Scheme == Uri.UriSchemeHttps;

        /// <summary>
    /// Resolve method.
    /// </summary>
public EmbedResolvedUrl Resolve(Uri uri) => new(
        EmbedSrc: uri.ToString(),
        DefaultRatio: AspectRatio.Widescreen,
        DefaultSandbox: SandboxFlags.Strict);
}
