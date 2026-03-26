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
}
