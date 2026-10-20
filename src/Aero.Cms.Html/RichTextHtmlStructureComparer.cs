using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace Aero.Cms.Html;

/// <summary>
/// Compares rich-text fragments without treating translated text as immutable markup.
/// </summary>
public static class RichTextHtmlStructureComparer
{
    /// <summary>
    /// Returns <see langword="true"/> when both fragments satisfy the CMS rich-text
    /// allowlist and retain the same element, attribute, and meaningful text-node shape.
    /// Text-node values may differ because translating them is the purpose of the operation.
    /// </summary>
    public static bool PreservesStructure(string? source, string? translated)
    {
        if (!TryGetSafeStructureFingerprint(source, out var sourceFingerprint) ||
            !TryGetSafeStructureFingerprint(translated, out var translatedFingerprint))
        {
            return false;
        }

        return string.Equals(sourceFingerprint, translatedFingerprint, StringComparison.Ordinal);
    }

    /// <summary>
    /// Creates a structural fingerprint only when the fragment is unchanged by the
    /// CMS rich-text sanitizer.
    /// </summary>
    public static bool TryGetSafeStructureFingerprint(string? html, out string fingerprint)
    {
        if (!TryParseSafeFragment(html, out var body))
        {
            fingerprint = string.Empty;
            return false;
        }

        fingerprint = string.Join('\u001f', StructureTokens(body.ChildNodes));
        return true;
    }

    /// <summary>Creates a stable structural fingerprint for an already-safe HTML fragment.</summary>
    public static string StructureFingerprint(string? html)
    {
        var body = ParseFragment(html);
        return string.Join('\u001f', StructureTokens(body.ChildNodes));
    }

    private static bool TryParseSafeFragment(string? html, out IElement body)
    {
        var value = html ?? string.Empty;
        body = ParseFragment(value);

        var sanitizedBody = ParseFragment(RichTextHtmlSanitizer.Create().Sanitize(value));
        return CompleteTokens(body.ChildNodes)
            .SequenceEqual(CompleteTokens(sanitizedBody.ChildNodes), StringComparer.Ordinal);
    }

    private static IElement ParseFragment(string? html)
    {
        var parser = new HtmlParser();
        var document = parser.ParseDocument("<html><body></body></html>");
        var body = document.Body ?? throw new InvalidOperationException("An HTML body could not be created.");

        foreach (var node in parser.ParseFragment(html ?? string.Empty, body).ToArray())
        {
            body.AppendChild(node);
        }

        return body;
    }

    private static IEnumerable<string> StructureTokens(IEnumerable<INode> nodes)
    {
        foreach (var node in nodes)
        {
            foreach (var token in Tokens(node, includeTextValues: false))
            {
                yield return token;
            }
        }
    }

    private static IEnumerable<string> CompleteTokens(IEnumerable<INode> nodes)
    {
        foreach (var node in nodes)
        {
            foreach (var token in Tokens(node, includeTextValues: true))
            {
                yield return token;
            }
        }
    }

    private static IEnumerable<string> Tokens(INode node, bool includeTextValues)
    {
        if (node is IElement element)
        {
            yield return $"element-open:{element.LocalName}";
            foreach (var attribute in element.Attributes.OrderBy(attribute => attribute.Name, StringComparer.Ordinal))
            {
                yield return $"attribute:{attribute.Name}={attribute.Value}";
            }

            foreach (var child in element.ChildNodes)
            {
                foreach (var token in Tokens(child, includeTextValues))
                {
                    yield return token;
                }
            }

            yield return $"element-close:{element.LocalName}";
            yield break;
        }

        if (node is IText text)
        {
            if (!string.IsNullOrWhiteSpace(text.Data))
            {
                yield return includeTextValues ? $"text:{text.Data}" : "text";
            }

            yield break;
        }

        if (node is IComment comment)
        {
            yield return includeTextValues ? $"comment:{comment.Data}" : "comment";
            yield break;
        }

        yield return $"node:{node.NodeType}";
    }
}
