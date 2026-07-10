using Aero.Cms.Abstractions.Ai;

namespace Aero.Cms.Modules.Ai.Services;

/// <summary>
/// Defines an interface for IEnhanceContentPromptBuilder.
/// </summary>
public interface IEnhanceContentPromptBuilder
{
        /// <summary>
    /// Build method.
    /// </summary>
string Build(EnhanceContentRequest request);
}
