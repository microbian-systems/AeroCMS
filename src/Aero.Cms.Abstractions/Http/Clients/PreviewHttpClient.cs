namespace Aero.Cms.Abstractions.Http.Clients;

using Aero.Core.Railway;
using Microsoft.Extensions.Logging;

/// <summary>
/// Interface for preview HTTP client.
/// </summary>
public interface IPreviewHttpClient
{
    /// <summary>
    /// Previews a page by its identifier.
    /// </summary>
    /// <param name="id">The page identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The preview object or an error.</returns>
    Task<Result<object, AeroError>> PreviewPageAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// Previews a blog post by its identifier.
    /// </summary>
    /// <param name="id">The blog post identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The preview object or an error.</returns>
    Task<Result<object, AeroError>> PreviewBlogPostAsync(long id, CancellationToken ct = default);
}

/// <summary>
/// Typed client for preview endpoints.
/// </summary>
public class PreviewHttpClient(HttpClient httpClient, ILogger<PreviewHttpClient> logger) 
    : AeroCmsClientBase(httpClient, logger), IPreviewHttpClient
{
    /// <inheritdoc />
    public override string Path => "admin/preview";

    /// <inheritdoc />
    public Task<Result<object, AeroError>> PreviewPageAsync(long id, CancellationToken ct = default)
    {
        return GetAsync<object>($"pages/{id}", ct);
    }

    /// <inheritdoc />
    public Task<Result<object, AeroError>> PreviewBlogPostAsync(long id, CancellationToken ct = default)
    {
        return GetAsync<object>($"blog-posts/{id}", ct);
    }
}
