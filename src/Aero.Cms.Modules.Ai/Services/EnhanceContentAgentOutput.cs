namespace Aero.Cms.Modules.Ai.Services;

internal sealed record EnhanceContentAgentOutput(
    string EnhancedText,
    string? Rationale,
    IReadOnlyList<string>? Warnings);
