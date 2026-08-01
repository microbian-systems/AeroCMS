namespace Aero.Cms.Modules.Ai.Services;

/// <summary>
/// Represents the structured fields expected from a document-translation provider response.
/// </summary>
/// <param name="Fields">The provider-generated translations keyed by requested field identifier.</param>
/// <param name="Warnings">Optional provider-generated warnings for the caller.</param>
internal sealed record TranslateDocumentAgentOutput(
    IReadOnlyDictionary<string, string>? Fields,
    IReadOnlyList<string>? Warnings);
