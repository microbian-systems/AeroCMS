namespace Aero.Cms.Modules.Docs.Areas.Docs.Models;

/// <summary>
/// Describes a Markdown heading included in the on-page navigation.
/// </summary>
public sealed class HeadingItem
{
    /// <summary>
    /// Gets or sets the heading text presented to the user.
    /// </summary>
public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the generated HTML anchor identifier without a leading hash.
    /// </summary>
public string AnchorId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the entry targets an H2 (2) or H3 (3) element.
    /// </summary>
    /// <remarks>Values produced by <see cref="HeadingExtractor"/> are limited to 2 and 3.</remarks>
public int Level { get; set; } // 2 = H2, 3 = H3
}
