namespace Aero.Cms.Html;

/// <summary>
/// The result of evaluating whether one node may become a child of another.
/// </summary>
public sealed record HtmlContentPolicyDecision(bool IsAllowed, string? Reason)
{
    public static HtmlContentPolicyDecision Allow() => new(true, null);

    public static HtmlContentPolicyDecision Deny(string reason) => new(false, reason);
}
