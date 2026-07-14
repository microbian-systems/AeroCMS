namespace Aero.Cms.Html;

/// <summary>
/// Editable properties of an HTML element, excluding its stable identity,
/// tag, and node kind. Text-only elements may opt into replacing their literal
/// child content as part of the same atomic property command.
/// </summary>
public sealed class HtmlNodeProperties
{
    public Dictionary<string, string> Attributes { get; set; } = [];

    public List<string> ThemeClasses { get; set; } = [];

    public HtmlStyle? Style { get; set; }

    public bool ReplaceChildrenWithLiteralText { get; set; }

    public string? LiteralText { get; set; }

    public static HtmlNodeProperties From(HtmlNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        return new HtmlNodeProperties
        {
            Attributes = new Dictionary<string, string>(node.Attributes, StringComparer.Ordinal),
            ThemeClasses = [.. node.ThemeClasses],
            Style = HtmlTreeOperations.CloneStyle(node.Style)
        };
    }
}
