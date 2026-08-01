namespace Aero.Cms.Html;

/// <summary>
/// The page-content aggregate value persisted within a page draft or publication snapshot.
/// </summary>
public sealed class HtmlPageContent
{
    /// <summary>
    /// Gets or sets the non-rendered root fragment that owns the page's ordered top-level nodes.
    /// </summary>
    public HtmlNode Root { get; set; } = HtmlNode.CreateFragment();
}
