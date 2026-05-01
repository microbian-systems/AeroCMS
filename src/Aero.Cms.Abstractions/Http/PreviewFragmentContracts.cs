using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Layout;

namespace Aero.Cms.Abstractions.Http;

/// <summary>
/// Request payload for rendering an unsaved page preview fragment.
/// </summary>
public sealed record PreviewPageFragmentRequest(
    IReadOnlyList<EditorBlock>? Blocks = null,
    IReadOnlyList<LayoutRegion>? LayoutRegions = null);

/// <summary>
/// Response payload for a rendered page preview fragment.
/// </summary>
public sealed record PreviewPageFragmentResponse(string Html);

/// <summary>
/// Request payload for rendering an unsaved blog post preview fragment.
/// </summary>
public sealed record PreviewBlogPostFragmentRequest(IReadOnlyList<BlockBase>? Content = null);

/// <summary>
/// Response payload for a rendered blog post preview fragment.
/// </summary>
public sealed record PreviewBlogPostFragmentResponse(string Html);

/// <summary>
/// Request payload for rendering an unsaved single block preview fragment.
/// </summary>
public sealed record PreviewBlockFragmentRequest(BlockBase? Block);

/// <summary>
/// Response payload for a rendered single block preview fragment.
/// </summary>
public sealed record PreviewBlockFragmentResponse(string Html);
