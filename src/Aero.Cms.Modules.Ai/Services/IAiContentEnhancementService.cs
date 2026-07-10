using Aero.Cms.Abstractions.Ai;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Modules.Ai.Services;

/// <summary>
/// Defines an interface for IAiContentEnhancementService.
/// </summary>
public interface IAiContentEnhancementService
{
        /// <summary>
    /// EnhanceAsync method.
    /// </summary>
Task<Result<EnhanceContentResponse>> EnhanceAsync(
        EnhanceContentRequest request,
        CancellationToken cancellationToken = default);
}
