using Aero.Cms.Abstractions.Ai;

namespace Aero.Cms.Modules.Ai.Services;

/// <summary>
/// Builds the provider-facing user prompt for document translation.
/// </summary>
public interface ITranslateDocumentPromptBuilder
{
        /// <summary>
    /// Serializes cultures, field keys, hints, and source text into prompt text.
    /// </summary>
    /// <param name="request">The translation request whose fields will be included.</param>
    /// <returns>A prompt that requests a JSON field map and warnings collection.</returns>
    /// <remarks>
    /// Implementations construct provider input; they do not establish that field content is trusted,
    /// sanitized, confidential, or safe to send to the configured provider.
    /// </remarks>
string Build(TranslateDocumentRequest request);
}
