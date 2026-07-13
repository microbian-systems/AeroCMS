namespace Aero.Cms.Html;

/// <summary>
/// Applies the first-release manifest and focused HTML nesting rules.
/// </summary>
public sealed class HtmlContentModelPolicy(HtmlElementCatalog catalog) : IHtmlContentModelPolicy
{
    public HtmlContentPolicyDecision CanContain(HtmlNode parent, HtmlNode child)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(child);

        if (parent.Kind == HtmlNodeKind.Fragment)
        {
            return child.Kind == HtmlNodeKind.Element && catalog.TryGet(child.TagName, out _)
                ? HtmlContentPolicyDecision.Allow()
                : HtmlContentPolicyDecision.Deny("The page fragment can contain supported element nodes only.");
        }

        if (parent.Kind is not HtmlNodeKind.Element
            || !catalog.TryGet(parent.TagName, out var parentDefinition)
            || parentDefinition is null)
        {
            return HtmlContentPolicyDecision.Deny("The parent is not a supported HTML element.");
        }

        if (parentDefinition!.IsVoid)
        {
            return HtmlContentPolicyDecision.Deny($"<{parentDefinition.Tag}> is a void element and cannot have children.");
        }

        if (child.Kind == HtmlNodeKind.Text)
        {
            return parentDefinition.ChildModel is HtmlChildModel.Flow or HtmlChildModel.Phrasing
                ? HtmlContentPolicyDecision.Allow()
                : HtmlContentPolicyDecision.Deny($"<{parentDefinition.Tag}> cannot contain literal text directly.");
        }

        if (child.Kind is not HtmlNodeKind.Element
            || !catalog.TryGet(child.TagName, out var childDefinition)
            || childDefinition is null)
        {
            return HtmlContentPolicyDecision.Deny("The child is not a supported HTML element.");
        }

        if (parentDefinition.IsInteractive && childDefinition!.IsInteractive)
        {
            return HtmlContentPolicyDecision.Deny("Interactive elements cannot be nested inside interactive elements.");
        }

        return parentDefinition.ChildModel switch
        {
            HtmlChildModel.Flow when childDefinition.IsFlowContent => HtmlContentPolicyDecision.Allow(),
            HtmlChildModel.Phrasing when childDefinition.IsPhrasingContent => HtmlContentPolicyDecision.Allow(),
            HtmlChildModel.List when string.Equals(childDefinition.Tag, "li", StringComparison.OrdinalIgnoreCase)
                => HtmlContentPolicyDecision.Allow(),
            _ => HtmlContentPolicyDecision.Deny($"<{childDefinition.Tag}> is not allowed inside <{parentDefinition.Tag}>.")
        };
    }
}
