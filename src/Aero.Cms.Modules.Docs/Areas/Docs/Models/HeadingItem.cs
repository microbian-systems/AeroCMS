namespace Aero.Cms.Modules.Docs.Areas.Docs.Models;

/// <summary>
/// Heading extracted from Markdown AST for "On This Page" navigation.
/// </summary>
public sealed class HeadingItem
{
    public string Text { get; set; } = string.Empty;
    public string AnchorId { get; set; } = string.Empty;
    public int Level { get; set; } // 2 = H2, 3 = H3
}
