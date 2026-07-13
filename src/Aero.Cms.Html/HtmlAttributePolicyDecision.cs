namespace Aero.Cms.Html;

/// <summary>
/// Result of validating one emitted HTML attribute.
/// </summary>
public sealed record HtmlAttributePolicyDecision(bool IsAllowed, string? Reason)
{
    /// <summary>
    /// Creates an allowed attribute decision.
    /// </summary>
    public static HtmlAttributePolicyDecision Allow() => new(true, null);

    /// <summary>
    /// Creates a rejected attribute decision.
    /// </summary>
    public static HtmlAttributePolicyDecision Deny(string reason) => new(false, reason);
}
