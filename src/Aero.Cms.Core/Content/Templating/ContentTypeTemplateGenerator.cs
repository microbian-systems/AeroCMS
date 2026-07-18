using System.Text.RegularExpressions;
using Aero.Cms.Abstractions.Content;

namespace Aero.Cms.Core.Content.Templating;

/// <summary>
/// Represents a class for ContentTypeTemplateGenerator.
/// </summary>
public static class ContentTypeTemplateGenerator
{
    private static readonly Regex SafeName = new("^[a-zA-Z_][a-zA-Z0-9_]*$", RegexOptions.Compiled);
    private static readonly Regex LegacyComment = new(
        @"\{\{\*(.*?)\*\}\}",
        RegexOptions.Compiled | RegexOptions.Singleline,
        TimeSpan.FromSeconds(1));

        /// <summary>
    /// GenerateTemplate method.
    /// </summary>
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
    /// ScribanAccessor method.
    /// </summary>
public static string ScribanAccessor(string fieldName)
        => SafeName.IsMatch(fieldName)
            ? "fields." + fieldName
            : "fields[\"" + fieldName + "\"]";

        /// <summary>
    /// NormalizeFieldAccessors method.
    /// </summary>
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
    /// NormalizeTemplate method.
    /// </summary>
public static string NormalizeTemplate(
        string template,
        IEnumerable<ContentFieldDefinition> fields)
    {
        template = LegacyComment.Replace(template, "{{##$1##}}");
        return NormalizeFieldAccessors(template, fields);
    }
}

internal sealed class DefaultFieldSnippet(string fieldType) : IFieldTemplateSnippet
{
        /// <summary>
    /// Gets or sets the Field Type.
    /// </summary>
public string FieldType => fieldType;
        /// <summary>
    /// Render method.
    /// </summary>
public string Render(ContentFieldDefinition field)
    {
        var a = ContentTypeTemplateGenerator.ScribanAccessor(field.Name);
        return "<div class=\"aero-field aero-field-" + fieldType + "\">{{" + a + "}}</div>";
    }
}
