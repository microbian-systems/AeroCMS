namespace Aero.Cms.Abstractions.Ai;

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
/// Identifies one event in a streamed content-enhancement response.
/// </summary>
public enum EnhanceContentEventKind
{
    /// <summary>The stream has started and provider metadata is available.</summary>
    Metadata = 0,

    /// <summary>A validated fragment of the proposed field text is available for preview.</summary>
    Delta = 1,

    /// <summary>The provider output has been fully parsed and is ready for review.</summary>
    Complete = 2,

    /// <summary>The stream failed after its HTTP response had already started.</summary>
    Error = 3
}

/// <summary>
/// Typed POST-SSE event emitted while an AI content suggestion is generated.
/// </summary>
/// <param name="Kind">The event kind.</param>
/// <param name="Text">A text delta or a safe error message.</param>
/// <param name="Response">The fully parsed response, present only for a successful completion.</param>
/// <param name="CorrelationId">The server correlation identifier for diagnostics.</param>
/// <param name="Provider">The selected provider display name.</param>
/// <param name="Model">The selected provider model.</param>
public sealed record EnhanceContentEvent(
    EnhanceContentEventKind Kind,
    string? Text = null,
    EnhanceContentResponse? Response = null,
    string? CorrelationId = null,
    string? Provider = null,
    string? Model = null);

/// <summary>
/// Optional provider token usage metadata.
/// </summary>
public sealed record AiUsage(
    int? InputTokens,
    int? OutputTokens,
    int? TotalTokens);
