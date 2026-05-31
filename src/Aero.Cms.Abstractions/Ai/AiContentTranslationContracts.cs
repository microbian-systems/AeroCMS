using System.Text.Json.Serialization;

namespace Aero.Cms.Abstractions.Ai;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ContentFieldHint
{
    Title,
    Summary,
    Excerpt,
    SeoTitle,
    SeoDescription,
    MarkdownContent,
    Label,
    AltText,
    CompanyName,
    Tagline,
    CopyrightText,
    GroupName,
    CategoryName,
    CategoryDescription,
    TagName,
    TagDescription,
    BlockText,
    BlockCaption,
    BlockPlaceholder
}

public static class ContentFieldHintExtensions
{
    public static bool IsMarkdown(this ContentFieldHint hint) =>
        hint is ContentFieldHint.MarkdownContent;
}

public sealed record TranslateDocumentField(
    string Key,
    ContentFieldHint Hint,
    string SourceText);

public sealed record TranslateDocumentRequest(
    IReadOnlyList<TranslateDocumentField> Fields,
    string SourceCulture,
    string TargetCulture,
    string? ProviderId = null);

public sealed record TranslateDocumentResponse(
    IReadOnlyDictionary<string, string> TranslatedFields,
    IReadOnlyList<string> Warnings,
    string Provider,
    string Model);
