using Aero.Core.Railway;

namespace Aero.Cms.Abstractions.Ai;

/// <summary>
/// Defines an interface for IAiContentTranslationService.
/// </summary>
public interface IAiContentTranslationService
{
        /// <summary>
    /// TranslateAsync method.
    /// </summary>
Task<Result<TranslateDocumentResponse>> TranslateAsync(
        TranslateDocumentRequest request,
        CancellationToken cancellationToken = default);
}
