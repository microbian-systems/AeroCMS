using System.Text.Json;
using Aero.Cms.Abstractions.Ai;

namespace Aero.Cms.Modules.Ai.Services;

public sealed class TranslateDocumentPromptBuilder : ITranslateDocumentPromptBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public string Build(TranslateDocumentRequest request)
    {
        var payload = JsonSerializer.Serialize(new
        {
            request.SourceCulture,
            request.TargetCulture,
            Fields = request.Fields.Select(field => new
            {
                field.Key,
                Hint = field.Hint.ToString(),
                IsMarkdown = field.Hint.IsMarkdown(),
                field.SourceText
            })
        }, JsonOptions);

        return $$"""
            Translate the AeroCMS content fields from {{request.SourceCulture}} to {{request.TargetCulture}}.

            Return only a JSON object with:
            - fields: an object whose property names exactly match the input field keys and whose values are translated strings
            - warnings: an array of concise warnings, empty when there are none

            Rules:
            - Do not include markdown fences, comments, or preamble.
            - Preserve all input field keys exactly.
            - Preserve markdown structure, code blocks, links, HTML tags, and front matter for markdown fields.
            - Do not translate URLs, source paths, IDs, code, CSS classes, or brand names.
            - Keep title and SEO fields concise while adapting wording for the target locale.
            - Keep empty source text empty.

            Input:
            {{payload}}
            """;
    }
}
