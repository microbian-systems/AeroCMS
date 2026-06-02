using System.Text.RegularExpressions;
using Aero.Cms.Abstractions.Content;

namespace Aero.Cms.Core.Blocks.Dynamic;

public static class ContentTypeTemplateGenerator
{
    private static readonly Regex SafeName = new("^[a-zA-Z_][a-zA-Z0-9_]*$", RegexOptions.Compiled);

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

    public static string ScribanAccessor(string fieldName)
        => SafeName.IsMatch(fieldName)
            ? "block." + fieldName
            : "block[\"" + fieldName + "\"]";
}

internal sealed class DefaultFieldSnippet(string fieldType) : IFieldTemplateSnippet
{
    public string FieldType => fieldType;
    public string Render(ContentFieldDefinition field)
    {
        var a = ContentTypeTemplateGenerator.ScribanAccessor(field.Name);
        return "<div class=\"aero-field aero-field-" + fieldType + "\">{{" + a + "}}</div>";
    }
}
