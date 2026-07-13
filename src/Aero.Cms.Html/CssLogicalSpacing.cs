namespace Aero.Cms.Html;

/// <summary>
/// Direction-neutral box spacing. Inline start/end follows the page writing direction.
/// </summary>
public sealed class CssLogicalSpacing
{
    public CssLength? BlockStart { get; set; }
    public CssLength? InlineEnd { get; set; }
    public CssLength? BlockEnd { get; set; }
    public CssLength? InlineStart { get; set; }
}
