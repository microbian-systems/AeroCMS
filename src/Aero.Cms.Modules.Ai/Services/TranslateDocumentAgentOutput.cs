namespace Aero.Cms.Modules.Ai.Services;

internal sealed record TranslateDocumentAgentOutput(
    IReadOnlyDictionary<string, string>? Fields,
    IReadOnlyList<string>? Warnings);
