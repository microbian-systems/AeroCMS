using Aero.Core;
using Aero.Core.Railway;
using Marten;

namespace Aero.Cms.Core.Blocks.Dynamic;

/// <summary>
/// Represents a class for MartenDynamicBlockDefinitionService.
/// </summary>
public sealed class MartenDynamicBlockDefinitionService(IDocumentSession session) : IDynamicBlockDefinitionService
{
        /// <summary>
    /// GetAsync method.
    /// </summary>
public async Task<Result<DynamicBlockDefinition, AeroError>> GetAsync(
        long definitionId,
        int version,
        CancellationToken cancellationToken = default)
    {
        if (definitionId <= 0)
        {
            return AeroError.ValidationError(["Dynamic template definition id is required."]);
        }

        if (version <= 0)
        {
            return AeroError.ValidationError(["Dynamic template definition version is required."]);
        }

        try
        {
            var definition = await session.Query<DynamicBlockDefinition>()
                .FirstOrDefaultAsync(
                    item => item.Id == definitionId && item.Version == version && item.IsPublished,
                    token: cancellationToken);

            return definition is null
                ? AeroError.NotFoundError($"Published dynamic template definition '{definitionId}' version '{version}' was not found.")
                : Prelude.Ok<DynamicBlockDefinition, AeroError>(definition);
        }
        catch (Exception ex)
        {
            return AeroError.DatabaseError(ex.Message);
        }
    }
}
