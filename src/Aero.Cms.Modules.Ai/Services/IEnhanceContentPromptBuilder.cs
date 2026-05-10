using Aero.Cms.Abstractions.Ai;

namespace Aero.Cms.Modules.Ai.Services;

public interface IEnhanceContentPromptBuilder
{
    string Build(EnhanceContentRequest request);
}
