using Aero.Cms.Abstractions.Blocks.Embed;

namespace Aero.Cms.Ui.Neo.Embed;

/// <summary>
/// Composite resolver that tries each registered <see cref="IEmbedUrlResolver"/>
/// in order and returns the first successful resolution.
/// Falls back to <see cref="GenericIframeResolver"/> if no provider matches.
/// </summary>
public sealed class EmbedResolverPipeline
{
    private readonly IReadOnlyList<IEmbedUrlResolver> _resolvers;

    public EmbedResolverPipeline(IEnumerable<IEmbedUrlResolver> resolvers)
    {
        _resolvers = resolvers.ToList();
    }

    /// <summary>
    /// Resolves a raw URL by trying each registered resolver in order.
    /// Returns null if the URL cannot be resolved.
    /// </summary>
    public EmbedResolvedUrl? Resolve(Uri uri)
    {
        foreach (var resolver in _resolvers)
        {
            if (resolver.CanResolve(uri))
                return resolver.Resolve(uri);
        }
        return null;
    }

    /// <summary>
    /// Returns the resolved URL if the host is on the allow-list; otherwise null.
    /// </summary>
    public EmbedResolvedUrl? ResolveSafe(Uri uri, EmbedAllowList allowList)
    {
        var resolved = Resolve(uri);
        if (resolved is null) return null;

        var resolvedUri = new Uri(resolved.EmbedSrc, UriKind.Absolute);
        return allowList.IsAllowed(resolvedUri) ? resolved : null;
    }
}
