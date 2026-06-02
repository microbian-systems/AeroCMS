using Aero.Core.Railway;

namespace Aero.Cms.Abstractions.Ai;

public interface IAiContentTranslationService
{
    Task<Result<TranslateDocumentResponse>> TranslateAsync(
        TranslateDocumentRequest request,
        CancellationToken cancellationToken = default);
}
