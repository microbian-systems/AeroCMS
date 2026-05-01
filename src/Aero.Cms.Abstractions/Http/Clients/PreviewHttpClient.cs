using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Layout;
using Aero.Cms.Abstractions.Http;
using Aero.Core.Railway;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Abstractions.Http.Clients;

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

    /// <summary>
    /// Renders an unsaved page document to an HTML fragment.
    /// </summary>
    Task<Result<string, AeroError>> RenderPageFragmentAsync(
        IReadOnlyList<EditorBlock>? blocks = null,
        IReadOnlyList<LayoutRegion>? layoutRegions = null,
        CancellationToken ct = default);

    /// <summary>
    /// Renders an unsaved blog post document to an HTML fragment.
    /// </summary>
    Task<Result<string, AeroError>> RenderBlogPostFragmentAsync(IReadOnlyList<BlockBase> content, CancellationToken ct = default);

    /// <summary>
    /// Renders an unsaved block to an HTML fragment.
    /// </summary>
    Task<Result<string, AeroError>> RenderBlockFragmentAsync(BlockBase block, CancellationToken ct = default);
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

    /// <inheritdoc />
    public async Task<Result<string, AeroError>> RenderPageFragmentAsync(
        IReadOnlyList<EditorBlock>? blocks = null,
        IReadOnlyList<LayoutRegion>? layoutRegions = null,
        CancellationToken ct = default)
    {
        var result = await PostAsync<PreviewPageFragmentRequest, PreviewPageFragmentResponse>(
            "pages/render-fragment",
            new PreviewPageFragmentRequest(blocks, layoutRegions),
            ct);

        if (result is Result<PreviewPageFragmentResponse, AeroError>.Ok ok)
            return new Result<string, AeroError>.Ok(ok.Value.Html);
        return new Result<string, AeroError>.Failure(((Result<PreviewPageFragmentResponse, AeroError>.Failure)result).Error);
    }

    /// <inheritdoc />
    public async Task<Result<string, AeroError>> RenderBlogPostFragmentAsync(IReadOnlyList<BlockBase> content, CancellationToken ct = default)
    {
        var result = await PostAsync<PreviewBlogPostFragmentRequest, PreviewBlogPostFragmentResponse>(
            "blog-posts/render-fragment",
            new PreviewBlogPostFragmentRequest(content),
            ct);

        if (result is Result<PreviewBlogPostFragmentResponse, AeroError>.Ok ok)
            return new Result<string, AeroError>.Ok(ok.Value.Html);
        return new Result<string, AeroError>.Failure(((Result<PreviewBlogPostFragmentResponse, AeroError>.Failure)result).Error);
    }

    /// <inheritdoc />
    public async Task<Result<string, AeroError>> RenderBlockFragmentAsync(BlockBase block, CancellationToken ct = default)
    {
        var result = await PostAsync<PreviewBlockFragmentRequest, PreviewBlockFragmentResponse>(
            "blocks/render-fragment",
            new PreviewBlockFragmentRequest(block),
            ct);

        if (result is Result<PreviewBlockFragmentResponse, AeroError>.Ok ok)
            return new Result<string, AeroError>.Ok(ok.Value.Html);
        return new Result<string, AeroError>.Failure(((Result<PreviewBlockFragmentResponse, AeroError>.Failure)result).Error);
    }
}
