namespace Aero.Cms.Abstractions.Http.Clients;

using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Core.Blocks;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.Extensions.Logging;

/// <summary>
/// Interface for blocks HTTP client.
/// </summary>
public interface IBlocksHttpClient
{
    /// <summary>
    /// Gets a block by its identifier.
    /// </summary>
    /// <param name="id">The block identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A result containing the block or an error.</returns>
    Task<Result<BlockBase, AeroError>> GetByIdAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// Saves a block.
    /// </summary>
    /// <param name="block">The block to save.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A result containing the saved block or an error.</returns>
    Task<Result<BlockBase, AeroError>> SaveAsync(BlockBase block, CancellationToken ct = default);

    /// <summary>
    /// Delete a block by its identifier.
    /// </summary>
    /// <param name="id">The block identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A result containing true if successful, or an error.</returns>
    Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken ct = default);
}

/// <summary>
/// Typed client for blocks endpoints.
/// </summary>
public class BlocksHttpClient(HttpClient httpClient, ILogger<BlocksHttpClient> logger) 
    : AeroCmsClientBase(httpClient, logger), IBlocksHttpClient
{
    /// <inheritdoc />
    public override string Path => "admin/blocks";

    /// <inheritdoc />
    public Task<Result<BlockBase, AeroError>> GetByIdAsync(long id, CancellationToken ct = default)
    {
        return GetAsync<BlockBase>(id.ToString(), ct);
    }

    /// <inheritdoc />
    public Task<Result<BlockBase, AeroError>> SaveAsync(BlockBase block, CancellationToken ct = default)
    {
        return PostAsync<BlockBase, BlockBase>(string.Empty, block, ct);
    }

    /// <inheritdoc />
    public Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken ct = default)
    {
        return MapBoolResult(base.DeleteAsync(id.ToString(), ct));
    }

    private static async Task<Result<bool, AeroError>> MapBoolResult(Task<Result<HttpResponseMessage, AeroError>> task)
    {
        var response = await task;
        return response switch
        {
            Result<HttpResponseMessage, AeroError>.Ok => true,
            Result<HttpResponseMessage, AeroError>.Failure(var error) => error,
            _ => AeroError.CreateError("Unexpected result from HTTP operation")
        };
    }
}
