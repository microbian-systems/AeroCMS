namespace Aero.Cms.Html;

/// <summary>
/// Versioned canonical input for the editor catalog and future source generator.
/// </summary>
public sealed class HtmlElementManifest
{
    public int SchemaVersion { get; set; }
    public string CatalogVersion { get; set; } = string.Empty;
    public List<HtmlElementDefinition> Elements { get; set; } = [];
}
