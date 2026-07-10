using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Core.Blocks.Dynamic;

/// <summary>
/// Defines an interface for IDynamicBlockDefinitionService.
/// </summary>
public interface IDynamicBlockDefinitionService
{
        /// <summary>
    /// GetAsync method.
    /// </summary>
Task<Result<DynamicBlockDefinition, AeroError>> GetAsync(
        long definitionId,
        int version,
        CancellationToken cancellationToken = default);
}
