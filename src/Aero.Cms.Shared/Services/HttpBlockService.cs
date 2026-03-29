using System.Net.Http.Json;
using Aero.Cms.Core.Blocks;

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
            return await _httpClient.GetFromJsonAsync<BlockBase>($"/api/v1/blocks/{id}", ct);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<BlockBase> SaveAsync(BlockBase block, CancellationToken ct = default)
    {
        // On the client side, we usually save blocks as part of the page in a "one-shot" save.
        // However, we implement this to satisfy the interface.
        var response = await _httpClient.PostAsJsonAsync("/api/v1/blocks", block, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<BlockBase>(cancellationToken: ct))!;
    }
}
