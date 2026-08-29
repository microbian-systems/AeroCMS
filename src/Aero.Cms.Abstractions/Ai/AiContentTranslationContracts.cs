using System.Text.Json.Serialization;

namespace Aero.Cms.Abstractions.Ai;

/// <summary>
/// Defines an enumeration for ContentFieldHint.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ContentFieldHint
{
    Title,
    Summary,
    Excerpt,
    SeoTitle,
    SeoDescription,
    Slug,
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

/// <summary>
/// Represents a class for ContentFieldHintExtensions.
/// </summary>
public static class ContentFieldHintExtensions
{
        /// <summary>
    /// IsMarkdown method.
    /// </summary>
public static bool IsMarkdown(this ContentFieldHint hint) =>
        hint is ContentFieldHint.MarkdownContent;
}

/// <summary>
/// Represents a record for TranslateDocumentField.
/// </summary>
public sealed record TranslateDocumentField(
    string Key,
    ContentFieldHint Hint,
    string SourceText);

/// <summary>
/// Represents a record for TranslateDocumentRequest.
/// </summary>
public sealed record TranslateDocumentRequest(
    IReadOnlyList<TranslateDocumentField> Fields,
    string SourceCulture,
    string TargetCulture,
    string? ProviderId = null,
    IReadOnlyList<ContentTranslationPromptContext>? Context = null);

/// <summary>A bounded, non-content context entry supplied by a consuming host to improve translation terminology.</summary>
public sealed record ContentTranslationPromptContext(string Key, string Value);

/// <summary>
/// Represents a record for TranslateDocumentResponse.
/// </summary>
public sealed record TranslateDocumentResponse(
    IReadOnlyDictionary<string, string> TranslatedFields,
    IReadOnlyList<string> Warnings,
    string ProviderId,
    string ProviderLabel,
    string Model);
