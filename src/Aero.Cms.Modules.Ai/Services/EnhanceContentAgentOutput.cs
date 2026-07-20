namespace Aero.Cms.Modules.Ai.Services;

/// <summary>
/// Represents the structured fields expected from a content-enhancement provider response.
/// </summary>
/// <param name="EnhancedText">The provider-generated replacement text.</param>
/// <param name="Rationale">The provider's optional explanation of its edit.</param>
/// <param name="Warnings">Optional provider-generated warnings for the caller.</param>
internal sealed record EnhanceContentAgentOutput(
    string EnhancedText,
    string? Rationale,
    IReadOnlyList<string>? Warnings);
