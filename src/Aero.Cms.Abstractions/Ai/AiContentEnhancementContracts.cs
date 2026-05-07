namespace Aero.Cms.Abstractions.Ai;

/// <summary>
/// Supported AI provider modes for AeroCMS manager features.
/// </summary>
public enum AiProviderKind
{
    Tornado = 0,
    LmStudio = 1,
    OpenCode = 2,
    Future = 99
}

/// <summary>
/// Typed AI configuration loaded from manager settings and app configuration.
/// </summary>
public sealed record AiSettings(
    bool Enabled,
    AiProviderKind Provider,
    string? Endpoint,
    string? Model,
    string? ApiKeySecretName,
    string? ApiKeyEnvironmentVariable,
    float Temperature,
    int MaxOutputTokens,
    int TimeoutSeconds,
    bool StreamResponses,
    bool SaveUsageTelemetry);

/// <summary>
/// Request to enhance a single CMS content field.
/// </summary>
public sealed record EnhanceContentRequest(
    string ContentKind,
    string TargetField,
    string CurrentText,
    string? UserPrompt,
    string? Title,
    string? Summary,
    string? Slug,
    string? Tone,
    IReadOnlyDictionary<string, string>? Metadata);

/// <summary>
/// Response returned after AI-generated content is ready for manager review.
/// </summary>
public sealed record EnhanceContentResponse(
    string EnhancedText,
    string? Rationale,
    IReadOnlyList<string> Warnings,
    string Provider,
    string Model,
    AiUsage? Usage);

/// <summary>
/// Optional provider token usage metadata.
/// </summary>
public sealed record AiUsage(
    int? InputTokens,
    int? OutputTokens,
    int? TotalTokens);
