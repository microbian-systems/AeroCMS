namespace Aero.Cms.Abstractions.Blocks.Embed;

/// <summary>
/// The resolved output of an <see cref="IEmbedUrlResolver"/>.
/// Contains the final iframe src and provider-specific security/layout defaults.
/// </summary>
/// <param name="EmbedSrc">Final iframe src URL to render.</param>
/// <param name="DefaultRatio">Provider-recommended aspect ratio (e.g., 16:9 for video).</param>
/// <param name="DefaultSandbox">Provider-recommended sandbox flags.</param>
public sealed record EmbedResolvedUrl(
    string EmbedSrc,
    AspectRatio DefaultRatio,
    SandboxFlags DefaultSandbox);
