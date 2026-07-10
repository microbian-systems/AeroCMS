using Aero.Cms.Abstractions.Ai;

namespace Aero.Cms.Modules.Ai.Services;

/// <summary>
/// Defines an interface for ITranslateDocumentPromptBuilder.
/// </summary>
public interface ITranslateDocumentPromptBuilder
{
        /// <summary>
    /// Build method.
    /// </summary>
string Build(TranslateDocumentRequest request);
}
