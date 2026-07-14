namespace Aero.Cms.Html;

/// <summary>
/// First-release attribute allow-list for public HTML rendering.
/// CSS styling is intentionally modeled separately and raw inline styles are not accepted.
/// </summary>
public sealed class HtmlAttributePolicy : IHtmlAttributePolicy
{
    public HtmlAttributePolicyDecision CanRender(
        HtmlElementDefinition element,
        string attributeName,
        string attributeValue)
    {
        ArgumentNullException.ThrowIfNull(element);

        if (string.IsNullOrWhiteSpace(attributeName) || !IsValidAttributeName(attributeName))
        {
            return HtmlAttributePolicyDecision.Deny("The attribute name is invalid.");
        }

        if (attributeName.StartsWith("on", StringComparison.OrdinalIgnoreCase))
        {
            return HtmlAttributePolicyDecision.Deny("Inline event-handler attributes are not supported.");
        }

        if (string.Equals(attributeName, "style", StringComparison.OrdinalIgnoreCase))
        {
            return HtmlAttributePolicyDecision.Deny("Inline style attributes are not supported; use the style model instead.");
        }

        if (IsGlobalAttribute(attributeName)
            || element.AllowedAttributes.Contains(attributeName, StringComparer.OrdinalIgnoreCase))
        {
            if (!IsAllowedValue(element.Tag, attributeName, attributeValue))
            {
                return HtmlAttributePolicyDecision.Deny(
                    $"The {attributeName} value is not supported on <{element.Tag}>.");
            }

            return IsUrlAttribute(attributeName) && !IsSafeUrl(attributeName, attributeValue)
                ? HtmlAttributePolicyDecision.Deny($"The {attributeName} URL scheme is not supported.")
                : HtmlAttributePolicyDecision.Allow();
        }

        return HtmlAttributePolicyDecision.Deny($"The {attributeName} attribute is not supported on <{element.Tag}>.");
    }

    private static bool IsGlobalAttribute(string attributeName) =>
        attributeName.Equals("id", StringComparison.OrdinalIgnoreCase)
        || attributeName.Equals("class", StringComparison.OrdinalIgnoreCase)
        || attributeName.Equals("title", StringComparison.OrdinalIgnoreCase)
        || attributeName.Equals("role", StringComparison.OrdinalIgnoreCase)
        || attributeName.StartsWith("aria-", StringComparison.OrdinalIgnoreCase)
        || attributeName.StartsWith("data-", StringComparison.OrdinalIgnoreCase);

    private static bool IsUrlAttribute(string attributeName) =>
        attributeName.Equals("href", StringComparison.OrdinalIgnoreCase)
        || attributeName.Equals("src", StringComparison.OrdinalIgnoreCase)
        || attributeName.Equals("action", StringComparison.OrdinalIgnoreCase);

    private static bool IsSafeUrl(string attributeName, string value) =>
        attributeName.Equals("src", StringComparison.OrdinalIgnoreCase)
            ? HtmlUrlPolicy.IsSafeMediaUrl(value)
            : HtmlUrlPolicy.IsSafeNavigationUrl(value);

    private static bool IsAllowedValue(string tagName, string attributeName, string value)
    {
        if (attributeName.Equals("target", StringComparison.OrdinalIgnoreCase))
        {
            return value is "_self" or "_blank" or "_parent" or "_top";
        }

        if (tagName.Equals("img", StringComparison.OrdinalIgnoreCase)
            && attributeName.Equals("loading", StringComparison.OrdinalIgnoreCase))
        {
            return value is "lazy" or "eager";
        }

        if (tagName.Equals("button", StringComparison.OrdinalIgnoreCase)
            && attributeName.Equals("type", StringComparison.OrdinalIgnoreCase))
        {
            return value is "button" or "submit" or "reset";
        }

        if (tagName.Equals("form", StringComparison.OrdinalIgnoreCase)
            && attributeName.Equals("method", StringComparison.OrdinalIgnoreCase))
        {
            return value is "get" or "post";
        }

        if (tagName.Equals("input", StringComparison.OrdinalIgnoreCase)
            && attributeName.Equals("type", StringComparison.OrdinalIgnoreCase))
        {
            return value is "text" or "email" or "tel" or "url" or "number" or "password"
                or "checkbox" or "radio" or "date" or "time" or "datetime-local"
                or "month" or "week" or "color" or "range" or "hidden";
        }

        if (tagName.Equals("th", StringComparison.OrdinalIgnoreCase)
            && attributeName.Equals("scope", StringComparison.OrdinalIgnoreCase))
        {
            return value is "row" or "col" or "rowgroup" or "colgroup";
        }

        if (attributeName is "colspan" or "rowspan" or "width" or "height" or "rows" or "cols" or "maxlength")
        {
            return int.TryParse(value, out var number) && number > 0;
        }

        return true;
    }

    private static bool IsValidAttributeName(string value)
    {
        foreach (var character in value)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_' and not ':')
            {
                return false;
            }
        }

        return char.IsAsciiLetter(value[0]);
    }
}
