namespace Aero.Cms.Html;

/// <summary>
/// Direction-neutral box spacing. Inline start/end follows the page writing direction.
/// </summary>
public sealed class CssLogicalSpacing
{
    /// <summary>Gets or sets spacing before the box on its block axis.</summary>
    public CssLength? BlockStart { get; set; }
    /// <summary>Gets or sets spacing after the box on its inline axis.</summary>
    public CssLength? InlineEnd { get; set; }
    /// <summary>Gets or sets spacing after the box on its block axis.</summary>
    public CssLength? BlockEnd { get; set; }
    /// <summary>Gets or sets spacing before the box on its inline axis.</summary>
    public CssLength? InlineStart { get; set; }
}
