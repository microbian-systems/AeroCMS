namespace Aero.Cms.Html;

/// <summary>
/// Manifest metadata for one supported HTML element.
/// </summary>
public sealed class HtmlElementDefinition
{
    public string Tag { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PaletteCategory { get; set; } = string.Empty;
    public HtmlChildModel ChildModel { get; set; }
    public bool IsVoid { get; set; }
    public bool IsInteractive { get; set; }
    public bool IsPhrasingContent { get; set; }
    public bool IsFlowContent { get; set; }
    public List<string> StyleCapabilities { get; set; } = [];
}
