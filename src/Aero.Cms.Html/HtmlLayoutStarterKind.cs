namespace Aero.Cms.Html;

/// <summary>
/// Guided layout templates shown in the ordinary page-editor palette.
/// </summary>
public enum HtmlLayoutStarterKind
{
    /// <summary>A single full-width region.</summary>
    OneColumn,
    /// <summary>Two equal-width regions.</summary>
    TwoColumns,
    /// <summary>Three equal-width regions.</summary>
    ThreeColumns,
    /// <summary>Four equal-width regions.</summary>
    FourColumns,
    /// <summary>Two regions intended for asymmetric copy and media.</summary>
    Split,
    /// <summary>A heading followed by two content regions.</summary>
    HeadingTwoColumns,
    /// <summary>A responsive collection of card containers.</summary>
    CardGrid
}
