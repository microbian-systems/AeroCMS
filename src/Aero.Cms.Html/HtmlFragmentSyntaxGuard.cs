using System.Text;

namespace Aero.Cms.Html;

/// <summary>
/// Rejects source forms for which an HTML parser would otherwise recover, normalize, or discard intent.
/// </summary>
internal static class HtmlFragmentSyntaxGuard
{
    private static readonly HashSet<string> VoidTags = new(StringComparer.Ordinal)
    {
        "area", "base", "br", "col", "embed", "hr", "img", "input", "link", "meta", "param", "source", "track", "wbr"
    };

    /// <summary>
    /// Scans source syntax before parsing so parser recovery, normalization, and discarded constructs fail closed.
    /// </summary>
    internal static bool TryValidate(
        string source,
        HtmlFragmentImportLimits limits,
        out IReadOnlyList<HtmlFragmentSourceElement> elements,
        out string error)
    {
        elements = [];
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(source))
        {
            error = "The HTML fragment cannot be blank.";
            return false;
        }

        if (source.Length > limits.MaximumSourceLength)
        {
            error = $"The HTML fragment exceeds the maximum source length of {limits.MaximumSourceLength}.";
            return false;
        }

        if (source.Contains("<!--", StringComparison.Ordinal)
            || source.Contains("<!", StringComparison.Ordinal)
            || source.Contains("<?", StringComparison.Ordinal))
        {
            error = "Comments, declarations, doctypes, and processing instructions are not supported in imported fragments.";
            return false;
        }

        var parsedElements = new List<HtmlFragmentSourceElement>();
        var openElements = new Stack<string>();
        var index = 0;

        while (index < source.Length)
        {
            var open = source.IndexOf('<', index);
            if (open < 0)
            {
                break;
            }

            if (open + 1 >= source.Length || (source[open + 1] != '/' && !IsAsciiLetter(source[open + 1])))
            {
                error = "The HTML fragment contains malformed markup.";
                return false;
            }

            var close = FindTagEnd(source, open + 1);
            if (close < 0)
            {
                error = "The HTML fragment contains an unterminated tag.";
                return false;
            }

            var tagSource = source.AsSpan(open + 1, close - open - 1);
            if (tagSource.Length > 0 && tagSource[0] == '/')
            {
                if (!TryReadClosingTag(tagSource[1..], out var closingTag))
                {
                    error = "The HTML fragment contains a malformed closing tag.";
                    return false;
                }

                if (openElements.Count == 0 || !string.Equals(openElements.Peek(), closingTag, StringComparison.Ordinal))
                {
                    error = "The HTML fragment has mismatched or parser-recovered element nesting.";
                    return false;
                }

                openElements.Pop();
            }
            else
            {
                if (!TryReadOpeningTag(tagSource, out var tag, out var selfClosing, out var attributeError))
                {
                    error = attributeError;
                    return false;
                }

                if (selfClosing && !VoidTags.Contains(tag))
                {
                    error = $"<{tag}/> is not valid self-closing HTML and would require parser normalization.";
                    return false;
                }

                var parentTag = openElements.Count == 0 ? null : openElements.Peek();
                parsedElements.Add(new HtmlFragmentSourceElement(tag, parentTag));

                if (!selfClosing && !VoidTags.Contains(tag))
                {
                    openElements.Push(tag);
                    if (openElements.Count > limits.MaximumDepth)
                    {
                        error = $"The HTML fragment exceeds the maximum depth of {limits.MaximumDepth}.";
                        return false;
                    }
                }
            }

            index = close + 1;
        }

        if (openElements.Count > 0)
        {
            error = "The HTML fragment has unclosed elements and would require parser recovery.";
            return false;
        }

        elements = parsedElements;
        return true;
    }

    /// <summary>Finds the closing angle bracket while ignoring brackets inside quoted attribute values.</summary>
    private static int FindTagEnd(string source, int start)
    {
        char quote = '\0';
        for (var index = start; index < source.Length; index++)
        {
            var current = source[index];
            if (quote != '\0')
            {
                if (current == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (current is '\'' or '\"')
            {
                quote = current;
            }
            else if (current == '>')
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>Reads a canonical lower-case closing tag with no trailing syntax.</summary>
    private static bool TryReadClosingTag(ReadOnlySpan<char> source, out string tag)
    {
        tag = string.Empty;
        if (source.Length == 0 || !IsAsciiLetter(source[0]))
        {
            return false;
        }

        for (var index = 0; index < source.Length; index++)
        {
            if (!IsTagNameCharacter(source[index]))
            {
                return false;
            }
        }

        tag = source.ToString();
        return IsCanonicalLowerCase(tag);
    }

    /// <summary>Reads an opening tag while enforcing canonical names, unique attributes, and quoted values.</summary>
    private static bool TryReadOpeningTag(
        ReadOnlySpan<char> source,
        out string tag,
        out bool selfClosing,
        out string error)
    {
        tag = string.Empty;
        selfClosing = false;
        error = "The HTML fragment contains malformed markup.";

        var index = 0;
        if (source.Length == 0 || !IsAsciiLetter(source[index]))
        {
            return false;
        }

        var tagStart = index++;
        while (index < source.Length && IsTagNameCharacter(source[index])) index++;
        tag = source[tagStart..index].ToString();
        if (!IsCanonicalLowerCase(tag))
        {
            error = "Imported element tags must use canonical lower-case names.";
            return false;
        }

        var attributes = new HashSet<string>(StringComparer.Ordinal);
        while (index < source.Length)
        {
            SkipWhitespace(source, ref index);
            if (index == source.Length)
            {
                break;
            }

            if (source[index] == '/')
            {
                if (index != source.Length - 1)
                {
                    return false;
                }

                selfClosing = true;
                break;
            }

            if (!IsAsciiLetter(source[index]))
            {
                return false;
            }

            var attributeStart = index++;
            while (index < source.Length && IsAttributeNameCharacter(source[index])) index++;
            var attribute = source[attributeStart..index].ToString();
            if (!IsCanonicalLowerCase(attribute))
            {
                error = "Imported attribute names must use canonical lower-case names.";
                return false;
            }

            if (!attributes.Add(attribute))
            {
                error = $"The HTML fragment contains duplicate '{attribute}' attributes.";
                return false;
            }

            SkipWhitespace(source, ref index);
            if (index == source.Length || source[index] != '=')
            {
                continue;
            }

            index++;
            SkipWhitespace(source, ref index);
            if (index == source.Length || source[index] is not ('\'' or '\"'))
            {
                error = "Imported attribute values must be quoted.";
                return false;
            }

            var quote = source[index++];
            var valueStart = index;
            while (index < source.Length && source[index] != quote) index++;
            if (index == source.Length)
            {
                error = "The HTML fragment contains an unterminated attribute value.";
                return false;
            }

            _ = source[valueStart..index];
            index++;
        }

        return true;
    }

    /// <summary>Advances the caller's cursor across contiguous Unicode whitespace.</summary>
    private static void SkipWhitespace(ReadOnlySpan<char> source, ref int index)
    {
        while (index < source.Length && char.IsWhiteSpace(source[index])) index++;
    }

    /// <summary>Rejects uppercase source names that an HTML parser would silently normalize.</summary>
    private static bool IsCanonicalLowerCase(string value) => value.All(character => !char.IsUpper(character));

    /// <summary>Determines whether a character can begin a supported HTML name.</summary>
    private static bool IsAsciiLetter(char value) => value is >= 'a' and <= 'z' or >= 'A' and <= 'Z';

    /// <summary>Determines whether a character can continue a supported tag name.</summary>
    private static bool IsTagNameCharacter(char value) => IsAsciiLetter(value) || char.IsDigit(value) || value is '-';

    /// <summary>Determines whether a character can continue a supported attribute name.</summary>
    private static bool IsAttributeNameCharacter(char value) =>
        IsAsciiLetter(value) || char.IsDigit(value) || value is '-' or '_' or ':';
}

/// <summary>Captures source tag ancestry for comparison with the parser-produced DOM.</summary>
internal sealed record HtmlFragmentSourceElement(string TagName, string? ParentTagName);
