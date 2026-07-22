namespace Aero.Cms.Modules.Pages.Rendering;

/// <summary>
/// Explicit page metadata available to source-backed fragment renderers.
/// </summary>
public sealed record PageFragmentRenderContext
{
    /// <summary>Gets the authoritative site identifier.</summary>
    public long SiteId { get; init; }

    /// <summary>Gets the current page culture.</summary>
    public string Culture { get; init; } = string.Empty;

    /// <summary>Gets the optional page identifier.</summary>
    public long? PageId { get; init; }

    /// <summary>Gets the optional page title.</summary>
    public string? Title { get; init; }

    /// <summary>Gets the optional page slug.</summary>
    public string? Slug { get; init; }

    /// <summary>Gets the optional canonical page path.</summary>
    public string? Path { get; init; }
}
