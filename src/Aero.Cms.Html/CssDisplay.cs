namespace Aero.Cms.Html;

/// <summary>
/// Supported layout behaviors for rendered HTML elements.
/// </summary>
public enum CssDisplay
{
    /// <summary>Creates a block formatting box.</summary>
    Block,
    /// <summary>Creates an inline formatting box.</summary>
    Inline,
    /// <summary>Creates an inline-level block container.</summary>
    InlineBlock,
    /// <summary>Creates a block-level flex container.</summary>
    Flex,
    /// <summary>Creates an inline-level flex container.</summary>
    InlineFlex,
    /// <summary>Creates a block-level grid container.</summary>
    Grid,
    /// <summary>Creates an inline-level grid container.</summary>
    InlineGrid,
    /// <summary>Suppresses the element's layout box.</summary>
    None
}
