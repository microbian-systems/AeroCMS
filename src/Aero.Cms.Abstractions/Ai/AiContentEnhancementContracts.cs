namespace Aero.Cms.Abstractions.Ai;

/// <summary>
/// Supported AI provider modes for AeroCMS manager features.
/// </summary>
public enum AiProviderKind
{
    OpenAi = 0,
    Anthropic = 1,
    Google = 2,
    Groq = 3,
    DeepSeek = 4,
    MiniMax = 5,
    Mistral = 6,
    XAi = 7,
    Zai = 8,
    Perplexity = 9,
    Alibaba = 10,
    OpenRouter = 11,
    LmStudio = 50,
    OpenCode = 80,
    Future = 99
}

/// <summary>
/// Typed AI configuration loaded from manager settings and app configuration.
/// </summary>
public sealed record AiSettings(
    string ProviderId,
    string DisplayName,
    bool Enabled,
    AiProviderKind Provider,
    string? Endpoint,
    string? Model,
    string? ApiKeySecretName,
    string? ApiKeyEnvironmentVariable,
    bool HasApiKey,
    float Temperature,
    int MaxOutputTokens,
    int TimeoutSeconds,
    bool StreamResponses,
    bool SaveUsageTelemetry);

/// <summary>
/// Safe provider settings returned to manager UI clients.
/// </summary>
public sealed record AiProviderSettings(
    string Id,
    string DisplayName,
    AiProviderKind Provider,
    bool Enabled,
    bool IsDefault,
    string? Endpoint,
    string? Model,
    bool HasApiKey,
    float Temperature,
    int MaxOutputTokens,
    int TimeoutSeconds,
    bool StreamResponses,
    bool SaveUsageTelemetry,
    bool SupportsContentEnhancement);

/// <summary>
/// Update payload for one provider profile. The API key is write-only and is never returned by settings endpoints.
/// </summary>
public sealed record AiProviderSettingsUpdate(
    string Id,
    string DisplayName,
    AiProviderKind Provider,
    bool Enabled,
    string? Endpoint,
    string? Model,
    string? ApiKey,
    bool ClearApiKey,
    float Temperature,
    int MaxOutputTokens,
    int TimeoutSeconds,
    bool StreamResponses,
    bool SaveUsageTelemetry);

/// <summary>
/// Full AI settings response used by manager settings and editor provider pickers.
/// </summary>
public sealed record AiSettingsConfiguration(
    bool Enabled,
    string DefaultProviderId,
    IReadOnlyList<AiProviderSettings> Providers);

/// <summary>
/// Full AI settings update payload.
/// </summary>
public sealed record SaveAiSettingsRequest(
    bool Enabled,
    string DefaultProviderId,
    IReadOnlyList<AiProviderSettingsUpdate> Providers);

/// <summary>
/// Lightweight provider choice for content enhancement screens.
/// </summary>
public sealed record AiProviderOption(
    string Id,
    string DisplayName,
    AiProviderKind Provider,
    string? Model,
    bool IsDefault);

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
    IReadOnlyDictionary<string, string>? Metadata,
    string? ProviderId = null);

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
