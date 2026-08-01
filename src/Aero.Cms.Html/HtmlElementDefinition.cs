namespace Aero.Cms.Html;

/// <summary>
/// Manifest metadata for one supported HTML element.
/// </summary>
public sealed class HtmlElementDefinition
{
    /// <summary>Gets or sets the canonical lower-case HTML tag.</summary>
    public string Tag { get; set; } = string.Empty;
    /// <summary>Gets or sets the label presented to authors.</summary>
    public string DisplayName { get; set; } = string.Empty;
    /// <summary>Gets or sets the palette group used to organize the element.</summary>
    public string PaletteCategory { get; set; } = string.Empty;
    /// <summary>Gets or sets the broad child-content rule enforced for the element.</summary>
    public HtmlChildModel ChildModel { get; set; }
    /// <summary>Gets or sets whether HTML syntax forbids end tags and child content.</summary>
    public bool IsVoid { get; set; }
    /// <summary>Gets or sets whether the element participates in interactive-content restrictions.</summary>
    public bool IsInteractive { get; set; }
    /// <summary>Gets or sets whether the element can participate in phrasing content.</summary>
    public bool IsPhrasingContent { get; set; }
    /// <summary>Gets or sets whether the element can participate in flow content.</summary>
    public bool IsFlowContent { get; set; }
    /// <summary>Gets or sets whether authors may insert the element directly from the palette.</summary>
    public bool IsPaletteVisible { get; set; } = true;
    /// <summary>Gets or sets an explicit parent-tag allowlist applied in addition to the broad child model.</summary>
    public List<string> AllowedParentTags { get; set; } = [];
    /// <summary>Gets or sets an explicit child-tag allowlist applied in addition to the broad child model.</summary>
    public List<string> AllowedChildTags { get; set; } = [];
    /// <summary>Gets or sets element-specific attribute names accepted by the rendering policy.</summary>
    public List<string> AllowedAttributes { get; set; } = [];
    /// <summary>Gets or sets the semantic editor style groups supported by the element.</summary>
    public List<string> StyleCapabilities { get; set; } = [];
}
