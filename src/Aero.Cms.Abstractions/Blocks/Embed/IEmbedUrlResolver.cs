namespace Aero.Cms.Abstractions.Blocks.Embed;

/// <summary>
/// Resolves a raw user-provided URI into a normalized, security-safe embed URL.
/// Registered as DI singletons and composed by <see cref="EmbedResolverPipeline"/>.
/// Strategy pattern: each implementation handles one provider (YouTube, Vimeo, etc.).
/// </summary>
public interface IEmbedUrlResolver
{
    /// <summary>
    /// Returns true if this resolver can handle the given URI.
    /// </summary>
    bool CanResolve(Uri uri);

    /// <summary>
    /// Resolves the URI into a validated embed URL with provider-specific defaults.
    /// Called only when <see cref="CanResolve"/> returns true.
    /// </summary>
    EmbedResolvedUrl Resolve(Uri uri);
}
