using Aero.Cms.Core.Blocks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Aero.Cms.Modules.Headless.Api.v1;

public static class BlocksApi
{
    /// <summary>
    /// Maps the Blocks API endpoints.
    /// </summary>
    public static void MapBlocksApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/blocks")
            .WithTags("Admin - Blocks");

        group.MapGet("/{id:long}", GetBlockById)
            .WithName("GetBlockById");
    }

    private static async Task<IResult> GetBlockById(
        long id,
        IDocumentSession session,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(typeof(BlocksApi));
        try
        {
            var block = await session.LoadAsync<BlockBase>(id, cancellationToken);

            if (block is null)
            {
                return TypedResults.NotFound(new { error = $"Block with ID {id} not found." });
            }

            return TypedResults.Ok(block);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving block for id={Id}", id);
            return TypedResults.Problem(ex.Message);
        }
    }
}
