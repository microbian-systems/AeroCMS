using System.Text.Json;
using Aero.Cms.Abstractions.Ai;
using Aero.Cms.Abstractions.Content;

namespace Aero.Cms.Modules.Ai.Services;

/// <summary>Typed field adapter used to whitelist exactly which schema values can leave the CMS.</summary>
public interface IContentTranslationFieldHandler
{
    string FieldType { get; }
    bool TryCreate(ContentFieldDefinition definition, JsonElement source, out TranslateDocumentField field);
    bool IsSafeResult(string source, string translated);
}

public sealed class TextContentTranslationFieldHandler : IContentTranslationFieldHandler
{
    public string FieldType => ContentFieldTypes.Text;
    public bool TryCreate(ContentFieldDefinition definition, JsonElement source, out TranslateDocumentField field)
    {
        field = new(definition.Name, ContentFieldHint.BlockText, source.GetString() ?? string.Empty);
        return source.ValueKind == JsonValueKind.String;
    }
    public bool IsSafeResult(string source, string translated) => true;
}

public sealed class RichTextContentTranslationFieldHandler : IContentTranslationFieldHandler
{
    public string FieldType => ContentFieldTypes.RichText;
    public bool TryCreate(ContentFieldDefinition definition, JsonElement source, out TranslateDocumentField field)
    {
        field = new(definition.Name, ContentFieldHint.MarkdownContent, source.GetString() ?? string.Empty);
        return source.ValueKind == JsonValueKind.String;
    }
    public bool IsSafeResult(string source, string translated) => SchemaAwareContentAiTranslationGenerationService.PreservesMarkup(source, translated);
}
