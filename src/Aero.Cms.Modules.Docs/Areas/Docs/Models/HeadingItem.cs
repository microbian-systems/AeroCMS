namespace Aero.Cms.Modules.Docs.Areas.Docs.Models;

/// <summary>
/// Heading extracted from Markdown AST for "On This Page" navigation.
/// </summary>
public sealed class HeadingItem
{
        /// <summary>
    /// Gets or sets the Text.
    /// </summary>
public string Text { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Anchor Id.
    /// </summary>
public string AnchorId { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Level.
    /// </summary>
public int Level { get; set; } // 2 = H2, 3 = H3
}
