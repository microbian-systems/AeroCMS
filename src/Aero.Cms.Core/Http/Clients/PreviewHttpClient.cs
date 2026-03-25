namespace Aero.Cms.Core.Http.Clients;

using Aero.Core.Railway;
using Microsoft.Extensions.Logging;

public interface IPreviewHttpClient
{
    Task<Result<string, object>> PreviewPageAsync(long id, CancellationToken ct = default);
    Task<Result<string, object>> PreviewBlogPostAsync(long id, CancellationToken ct = default);
}

public class PreviewHttpClient(HttpClient httpClient, ILogger<PreviewHttpClient> logger) 
    : AeroCmsClientBase(httpClient, logger), IPreviewHttpClient
{
    protected override string ResourceName => "preview";

    public Task<Result<string, object>> PreviewPageAsync(long id, CancellationToken ct = default)
    {
        return GetResultAsync<object>($"pages/{id}", ct);
    }

    public Task<Result<string, object>> PreviewBlogPostAsync(long id, CancellationToken ct = default)
    {
        return GetResultAsync<object>($"blog-posts/{id}", ct);
    }
}
