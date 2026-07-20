using System.Text.RegularExpressions;
using Aero.Cms.Abstractions.Content;

namespace Aero.Cms.Core.Content.Templating;

/// <summary>
/// Generates and normalizes default Scriban templates for content types.
/// </summary>
public static class ContentTypeTemplateGenerator
{
    private static readonly Regex SafeName = new("^[a-zA-Z_][a-zA-Z0-9_]*$", RegexOptions.Compiled);
    private static readonly Regex LegacyComment = new(
        @"\{\{\*(.*?)\*\}\}",
        RegexOptions.Compiled | RegexOptions.Singleline,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// Generates a section containing one registered or fallback snippet per field.
    /// </summary>
    /// <param name="definition">The content type definition.</param>
    /// <param name="snippets">The snippets to select by field type, ignoring case.</param>
    /// <returns>The generated template text.</returns>
    /// <remarks>
    /// The content type alias, fallback field type, and snippet-generated labels or markup are
    /// inserted into HTML without HTML-attribute encoding. This generator is intended for
    /// trusted definitions. <see cref="SecureScribanRenderer"/> sanitizes the eventual output
    /// when it is used for rendering.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Multiple snippets register the same field type, ignoring case.
    /// </exception>
    public static string GenerateTemplate(ContentTypeDefinition definition, IEnumerable<IFieldTemplateSnippet> snippets)
    {
        var sb = new StringBuilder();
        var lookup = snippets.ToDictionary(s => s.FieldType, StringComparer.OrdinalIgnoreCase);

        sb.Append("<section class=\"content-type-").Append(definition.Alias).AppendLine("\">");

        foreach (var field in definition.Fields)
        {
            var snippet = lookup.TryGetValue(field.FieldType, out var s)
                ? s
                : new DefaultFieldSnippet(field.FieldType);
            sb.AppendLine(snippet.Render(field));
        }

        sb.AppendLine("</section>");
        return sb.ToString();
    }

    /// <summary>
    /// Formats a field lookup using dotted or bracketed Scriban syntax.
    /// </summary>
    /// <param name="fieldName">The field name.</param>
    /// <returns>Dotted syntax for an identifier-safe name; otherwise bracket syntax.</returns>
    /// <remarks>
    /// Bracketed names are wrapped in double quotes but embedded quotes and backslashes are
    /// not escaped. Callers must restrict field names accordingly.
    /// </remarks>
    public static string ScribanAccessor(string fieldName)
        => SafeName.IsMatch(fieldName)
            ? "fields." + fieldName
            : "fields[\"" + fieldName + "\"]";

    /// <summary>
    /// Rewrites dotted references for fields that require bracket syntax.
    /// </summary>
    /// <param name="template">The template text to rewrite.</param>
    /// <param name="fields">The field definitions whose names may occur in the template.</param>
    /// <returns>The rewritten template.</returns>
    /// <remarks>This is an ordinal text replacement, not a Scriban syntax-tree rewrite.</remarks>
    public static string NormalizeFieldAccessors(
        string template,
        IEnumerable<ContentFieldDefinition> fields)
    {
        foreach (var field in fields.Where(field => !SafeName.IsMatch(field.Name)))
        {
            template = template.Replace(
                "fields." + field.Name,
                ScribanAccessor(field.Name),
                StringComparison.Ordinal);
        }

        return template;
    }

    /// <summary>
    /// Converts legacy comment delimiters and normalizes field accessors.
    /// </summary>
    /// <param name="template">The template text to normalize.</param>
    /// <param name="fields">The content field definitions.</param>
    /// <returns>The normalized template.</returns>
    /// <remarks>
    /// Legacy comments are replaced with a timeout-limited regular expression before ordinal
    /// accessor replacement. The operation does not parse or validate the resulting template.
    /// </remarks>
    public static string NormalizeTemplate(
        string template,
        IEnumerable<ContentFieldDefinition> fields)
    {
        template = LegacyComment.Replace(template, "{{##$1##}}");
        return NormalizeFieldAccessors(template, fields);
    }
}

/// <summary>Generates the fallback field wrapper used when no specialized snippet is registered.</summary>
internal sealed class DefaultFieldSnippet(string fieldType) : IFieldTemplateSnippet
{
    /// <inheritdoc />
    public string FieldType => fieldType;

    /// <inheritdoc />
    public string Render(ContentFieldDefinition field)
    {
        var a = ContentTypeTemplateGenerator.ScribanAccessor(field.Name);
        return "<div class=\"aero-field aero-field-" + fieldType + "\">{{" + a + "}}</div>";
    }
}
