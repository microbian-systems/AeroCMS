namespace Aero.Cms.Html;

/// <summary>
/// Versioned canonical input for the editor catalog and future source generator.
/// </summary>
public sealed class HtmlElementManifest
{
    /// <summary>Gets or sets the shape version used to validate manifest compatibility.</summary>
    public int SchemaVersion { get; set; }
    /// <summary>Gets or sets the version of the catalog contents.</summary>
    public string CatalogVersion { get; set; } = string.Empty;
    /// <summary>Gets or sets the complete supported element definitions.</summary>
    public List<HtmlElementDefinition> Elements { get; set; } = [];
}
