namespace Aero.Cms.Html;

/// <summary>
/// Applies the first-release manifest and focused HTML nesting rules.
/// </summary>
/// <param name="catalog">The authoritative supported-element catalog.</param>
public sealed class HtmlContentModelPolicy(HtmlElementCatalog catalog) : IHtmlContentModelPolicy
{
    /// <inheritdoc />
    public HtmlContentPolicyDecision CanContain(HtmlNode parent, HtmlNode child)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(child);

        if (parent.Kind == HtmlNodeKind.Fragment)
        {
            if (child.Kind is not HtmlNodeKind.Element
                || !catalog.TryGet(child.TagName, out var rootChild)
                || rootChild is null
                || !rootChild.IsFlowContent
                || rootChild.AllowedParentTags.Count > 0)
            {
                return HtmlContentPolicyDecision.Deny("The page fragment can contain top-level flow elements only.");
            }

            return HtmlContentPolicyDecision.Allow();
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
            return parentDefinition.ChildModel is HtmlChildModel.Flow or HtmlChildModel.Phrasing or HtmlChildModel.Text
                ? HtmlContentPolicyDecision.Allow()
                : HtmlContentPolicyDecision.Deny($"<{parentDefinition.Tag}> cannot contain literal text directly.");
        }

        if (child.Kind is not HtmlNodeKind.Element
            || !catalog.TryGet(child.TagName, out var childDefinition)
            || childDefinition is null)
        {
            return HtmlContentPolicyDecision.Deny("The child is not a supported HTML element.");
        }

        var isDisclosureSummary = parentDefinition.Tag.Equals("details", StringComparison.OrdinalIgnoreCase)
            && childDefinition!.Tag.Equals("summary", StringComparison.OrdinalIgnoreCase);
        if (parentDefinition.IsInteractive && childDefinition.IsInteractive && !isDisclosureSummary)
        {
            return HtmlContentPolicyDecision.Deny("Interactive elements cannot be nested inside interactive elements.");
        }

        if (parentDefinition.Tag.Equals("form", StringComparison.OrdinalIgnoreCase)
            && childDefinition.Tag.Equals("form", StringComparison.OrdinalIgnoreCase))
        {
            return HtmlContentPolicyDecision.Deny("Forms cannot be nested inside forms.");
        }

        if (childDefinition.AllowedParentTags.Count > 0
            && !childDefinition.AllowedParentTags.Contains(parentDefinition.Tag, StringComparer.OrdinalIgnoreCase))
        {
            return HtmlContentPolicyDecision.Deny(
                $"<{childDefinition.Tag}> is only allowed inside {string.Join(", ", childDefinition.AllowedParentTags.Select(tag => $"<{tag}>"))}.");
        }

        if (isDisclosureSummary)
        {
            return HtmlContentPolicyDecision.Allow();
        }

        if (parentDefinition.AllowedChildTags.Count > 0)
        {
            return parentDefinition.AllowedChildTags.Contains(childDefinition.Tag, StringComparer.OrdinalIgnoreCase)
                ? HtmlContentPolicyDecision.Allow()
                : HtmlContentPolicyDecision.Deny($"<{childDefinition.Tag}> is not allowed inside <{parentDefinition.Tag}>.");
        }

        return parentDefinition.ChildModel switch
        {
            HtmlChildModel.Flow when childDefinition.IsFlowContent => HtmlContentPolicyDecision.Allow(),
            HtmlChildModel.Phrasing when childDefinition.IsPhrasingContent => HtmlContentPolicyDecision.Allow(),
            HtmlChildModel.List when string.Equals(childDefinition.Tag, "li", StringComparison.OrdinalIgnoreCase)
                => HtmlContentPolicyDecision.Allow(),
            HtmlChildModel.Elements => HtmlContentPolicyDecision.Deny(
                $"<{parentDefinition.Tag}> does not declare <{childDefinition.Tag}> as an allowed child."),
            HtmlChildModel.Text => HtmlContentPolicyDecision.Deny(
                $"<{parentDefinition.Tag}> can contain literal text only."),
            _ => HtmlContentPolicyDecision.Deny($"<{childDefinition.Tag}> is not allowed inside <{parentDefinition.Tag}>.")
        };
    }
}
