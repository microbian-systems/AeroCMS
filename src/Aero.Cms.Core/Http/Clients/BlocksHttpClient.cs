namespace Aero.Cms.Core.Http.Clients;

using Aero.Core.Railway;
using Microsoft.Extensions.Logging;
using Aero.Cms.Core.Blocks;

public interface IBlocksHttpClient
{
    Task<Result<string, BlockBase>> GetByIdAsync(long id, CancellationToken ct = default);
}

public class BlocksHttpClient(HttpClient httpClient, ILogger<BlocksHttpClient> logger) 
    : AeroCmsClientBase(httpClient, logger), IBlocksHttpClient
{
    protected override string ResourceName => "blocks";

    public Task<Result<string, BlockBase>> GetByIdAsync(long id, CancellationToken ct = default)
    {
        return GetResultAsync<BlockBase>(id.ToString(), ct);
    }
}
