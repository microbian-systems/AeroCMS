namespace Aero.Cms.Modules.Analytics;

/// <summary>
/// Identifies the document position for a rendered analytics snippet.
/// </summary>
public enum SeoScriptPlacement
{
    /// <summary>Inside the document <c>head</c>.</summary>
    Head,

    /// <summary>Immediately after the opening <c>body</c> tag.</summary>
    BodyStart,

    /// <summary>Immediately before the closing <c>body</c> tag.</summary>
    BodyEnd
}
