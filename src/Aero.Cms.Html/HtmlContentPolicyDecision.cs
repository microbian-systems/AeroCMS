namespace Aero.Cms.Html;

/// <summary>
/// The result of evaluating whether one node may become a child of another.
/// </summary>
/// <param name="IsAllowed">Whether the direct relationship is valid.</param>
/// <param name="Reason">The rejection reason, or <see langword="null"/> for an allowed relationship.</param>
public sealed record HtmlContentPolicyDecision(bool IsAllowed, string? Reason)
{
    /// <summary>Creates a successful containment decision.</summary>
    /// <returns>A decision with no rejection reason.</returns>
    public static HtmlContentPolicyDecision Allow() => new(true, null);

    /// <summary>Creates a rejected containment decision.</summary>
    /// <param name="reason">The user-facing or diagnostic explanation for the rejection.</param>
    /// <returns>A decision containing the supplied reason.</returns>
    public static HtmlContentPolicyDecision Deny(string reason) => new(false, reason);
}
