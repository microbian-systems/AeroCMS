using System.Text;
using Aero.Cms.Abstractions.Content;

namespace Aero.Cms.Core.Blocks.Dynamic;

public static class ContentTypeTemplateGenerator
{
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
}

internal sealed class DefaultFieldSnippet(string fieldType) : IFieldTemplateSnippet
{
    public string FieldType => fieldType;
    public string Render(ContentFieldDefinition field)
    {
        return new StringBuilder()
            .Append("<div class=\"aero-field aero-field-").Append(fieldType).Append("\">")
            .Append("{{ block.").Append(field.Name).Append(" }}</div>")
            .ToString();
    }
}
