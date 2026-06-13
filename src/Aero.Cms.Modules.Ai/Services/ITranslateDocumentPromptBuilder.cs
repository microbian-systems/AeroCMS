using Aero.Cms.Abstractions.Ai;

namespace Aero.Cms.Modules.Ai.Services;

public interface ITranslateDocumentPromptBuilder
{
    string Build(TranslateDocumentRequest request);
}
