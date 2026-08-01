namespace Aero.Cms.Html;

/// <summary>
/// Editable properties of an HTML element, excluding its stable identity,
/// tag, and node kind. Text-only elements may opt into replacing their literal
/// child content as part of the same atomic property command.
/// </summary>
public sealed class HtmlNodeProperties
{
    /// <summary>Gets or sets the candidate rendered attributes.</summary>
    public Dictionary<string, string> Attributes { get; set; } = [];

    /// <summary>Gets or sets optional allowlisted theme classes selected through advanced editing.</summary>
    public List<string> ThemeClasses { get; set; } = [];

    /// <summary>Gets or sets constrained semantic style intent.</summary>
    public HtmlStyle? Style { get; set; }

    /// <summary>Gets or sets whether an update replaces all existing children with one text node.</summary>
    public bool ReplaceChildrenWithLiteralText { get; set; }

    /// <summary>Gets or sets the literal replacement text when <see cref="ReplaceChildrenWithLiteralText"/> is enabled.</summary>
    public string? LiteralText { get; set; }

    /// <summary>
    /// Captures an independent editable-property value from an existing node.
    /// </summary>
    /// <param name="node">The node whose attributes, classes, and style are copied.</param>
    /// <returns>A mutable copy that does not share collections or style objects with <paramref name="node"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="node"/> is <see langword="null"/>.</exception>
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
