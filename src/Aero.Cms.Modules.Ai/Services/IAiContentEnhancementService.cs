using Aero.Cms.Abstractions.Ai;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Modules.Ai.Services;

public interface IAiContentEnhancementService
{
    Task<Result<EnhanceContentResponse, AeroError>> EnhanceAsync(
        EnhanceContentRequest request,
        CancellationToken cancellationToken = default);
}
