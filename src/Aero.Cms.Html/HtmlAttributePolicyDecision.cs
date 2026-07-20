namespace Aero.Cms.Html;

/// <summary>
/// Result of validating one emitted HTML attribute.
/// </summary>
/// <param name="IsAllowed">Whether the attribute may cross the rendering boundary.</param>
/// <param name="Reason">The rejection reason, or <see langword="null"/> for an allowed attribute.</param>
public sealed record HtmlAttributePolicyDecision(bool IsAllowed, string? Reason)
{
    /// <summary>
    /// Creates an allowed attribute decision.
    /// </summary>
    /// <returns>A decision with no rejection reason.</returns>
    public static HtmlAttributePolicyDecision Allow() => new(true, null);

    /// <summary>
    /// Creates a rejected attribute decision.
    /// </summary>
    /// <param name="reason">The user-facing or diagnostic explanation for the rejection.</param>
    /// <returns>A decision containing the supplied reason.</returns>
    public static HtmlAttributePolicyDecision Deny(string reason) => new(false, reason);
}
