using Aero.Core.Railway;
using Microsoft.Extensions.AI;

namespace Aero.Cms.Modules.AiAssistant;

/// <summary>
/// Supplies request-scoped tools to the manager assistant without requiring an HTTP loopback.
/// </summary>
public interface IAeroCmsAssistantToolProvider
{
    Task<Result<IReadOnlyList<AITool>>> CreateToolsAsync(
        CancellationToken cancellationToken = default);
}
