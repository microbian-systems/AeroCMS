using Aero.Cms.Abstractions.Ai;

namespace Aero.Cms.Modules.Ai.Services;

/// <summary>
/// Builds the provider-facing user prompt for content enhancement.
/// </summary>
public interface IEnhanceContentPromptBuilder
{
        /// <summary>
    /// Serializes a content-enhancement request into prompt text.
    /// </summary>
    /// <param name="request">The request whose content and editing instructions will be included.</param>
    /// <returns>A prompt that requests a JSON object containing enhanced text, rationale, and warnings.</returns>
    /// <remarks>
    /// Implementations construct provider input; they do not establish that request content is trusted,
    /// sanitized, confidential, or safe to send to the configured provider.
    /// </remarks>
string Build(EnhanceContentRequest request);
}
