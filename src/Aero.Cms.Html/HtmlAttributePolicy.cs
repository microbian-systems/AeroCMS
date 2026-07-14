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

            return attributeName.Equals("srcset", StringComparison.OrdinalIgnoreCase)
                && !IsSafeSourceSet(attributeValue)
                ? HtmlAttributePolicyDecision.Deny("The srcset value contains an unsupported media URL or descriptor.")
                : IsUrlAttribute(attributeName) && !IsSafeUrl(attributeName, attributeValue)
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
        || attributeName.Equals("poster", StringComparison.OrdinalIgnoreCase)
        || attributeName.Equals("action", StringComparison.OrdinalIgnoreCase)
        || attributeName.Equals("cite", StringComparison.OrdinalIgnoreCase);

    private static bool IsSafeUrl(string attributeName, string value) =>
        attributeName.Equals("src", StringComparison.OrdinalIgnoreCase)
            || attributeName.Equals("poster", StringComparison.OrdinalIgnoreCase)
            ? HtmlUrlPolicy.IsSafeMediaUrl(value)
            : HtmlUrlPolicy.IsSafeNavigationUrl(value);

    private static bool IsSafeSourceSet(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        foreach (var rawCandidate in value.Split(',', StringSplitOptions.TrimEntries))
        {
            var parts = rawCandidate.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length is < 1 or > 2 || !HtmlUrlPolicy.IsSafeMediaUrl(parts[0]))
            {
                return false;
            }

            if (parts.Length == 2 && !IsSourceSetDescriptor(parts[1]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsSourceSetDescriptor(string descriptor)
    {
        if (descriptor.EndsWith('w'))
        {
            return int.TryParse(descriptor[..^1], out var width) && width > 0;
        }

        if (!descriptor.EndsWith('x'))
        {
            return false;
        }

        return decimal.TryParse(
            descriptor[..^1],
            System.Globalization.NumberStyles.AllowDecimalPoint,
            System.Globalization.CultureInfo.InvariantCulture,
            out var density) && density > 0;
    }

    private static bool IsAllowedValue(string tagName, string attributeName, string value)
    {
        if ((tagName is "time" or "del" or "ins")
            && attributeName.Equals("datetime", StringComparison.OrdinalIgnoreCase))
        {
            return !string.IsNullOrWhiteSpace(value);
        }

        if (tagName.Equals("data", StringComparison.OrdinalIgnoreCase)
            && attributeName.Equals("value", StringComparison.OrdinalIgnoreCase))
        {
            return !string.IsNullOrWhiteSpace(value);
        }

        if ((tagName is "progress" or "meter")
            && (attributeName is "value" or "max" or "min" or "low" or "high" or "optimum"))
        {
            return TryParseDecimal(value, out var number)
                && (!tagName.Equals("progress", StringComparison.OrdinalIgnoreCase)
                    || !attributeName.Equals("max", StringComparison.OrdinalIgnoreCase)
                    || number > 0);
        }

        if (attributeName.Equals("target", StringComparison.OrdinalIgnoreCase))
        {
            return value is "_self" or "_blank" or "_parent" or "_top";
        }

        if (tagName.Equals("img", StringComparison.OrdinalIgnoreCase)
            && attributeName.Equals("loading", StringComparison.OrdinalIgnoreCase))
        {
            return value is "lazy" or "eager";
        }

        if ((tagName is "audio" or "video")
            && attributeName.Equals("preload", StringComparison.OrdinalIgnoreCase))
        {
            return value is "none" or "metadata" or "auto";
        }

        if (tagName.Equals("track", StringComparison.OrdinalIgnoreCase)
            && attributeName.Equals("kind", StringComparison.OrdinalIgnoreCase))
        {
            return value is "subtitles" or "captions" or "descriptions" or "chapters" or "metadata";
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

    private static bool TryParseDecimal(string value, out decimal number) => decimal.TryParse(
        value,
        System.Globalization.NumberStyles.Number,
        System.Globalization.CultureInfo.InvariantCulture,
        out number);

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
