using System.Net.Http.Json;
using Aero.Cms.Abstractions.Blocks;

using Aero.Cms.Abstractions.Http.Clients;

namespace Aero.Cms.Shared.Services;

/// <summary>
/// Client-side implementation of <see cref="IBlockService"/> that fetches blocks via HTTP.
/// </summary>
public sealed class HttpBlockService : IBlockService
{
    private readonly HttpClient _httpClient;

    public HttpBlockService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<BlockBase?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<BlockBase>($"/{HttpConstants.ApiPrefix}blocks/{id}", ct);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    /// <summary>
    /// Client-side batch-load: issues individual HTTP requests per block ID.
    /// This is NOT optimized for bulk retrieval (unlike the server-side
    /// AeroBlockService which uses a single PostgreSQL query). The N+1
    /// public page rendering path runs server-side (static SSR), so this
    /// method exists only to satisfy the IBlockService contract in WASM
    /// contexts where bulk-load is not on the hot path.
    /// </summary>
    public async Task<IReadOnlyDictionary<long, BlockBase>> GetByIdsAsync(
        IEnumerable<long> ids, CancellationToken ct = default)
    {
        var result = new Dictionary<long, BlockBase>();
        foreach (var id in ids)
        {
            var block = await GetByIdAsync(id, ct);
            if (block is not null)
                result[id] = block;
        }
        return result;
    }

    public async Task<BlockBase> SaveAsync(BlockBase block, CancellationToken ct = default)
    {
        // On the client side, we usually save blocks as part of the page in a "one-shot" save.
        // However, we implement this to satisfy the interface.
        var response = await _httpClient.PostAsJsonAsync($"/{HttpConstants.ApiPrefix}blocks", block, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<BlockBase>(cancellationToken: ct))!;
    }
}
