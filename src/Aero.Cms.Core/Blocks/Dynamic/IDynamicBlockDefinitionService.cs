using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Core.Blocks.Dynamic;

public interface IDynamicBlockDefinitionService
{
    Task<Result<DynamicBlockDefinition, AeroError>> GetAsync(
        long definitionId,
        int version,
        CancellationToken cancellationToken = default);
}
