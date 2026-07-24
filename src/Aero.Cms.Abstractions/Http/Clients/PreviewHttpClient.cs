using Aero.Cms.Html;
using Aero.Cms.Abstractions.Pages.Composition;
using Microsoft.Extensions.Logging;
using System.Text.Json;

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
        HtmlPageContent content,
        CancellationToken ct = default);

    /// <summary>
    /// Renders an unsaved page document and its optional typed-content composition.
    /// </summary>
    Task<Result<string, AeroError>> RenderPageFragmentAsync(
        HtmlPageContent content,
        PageCompositionDocument? composition,
        string? culture,
        CancellationToken ct = default);

    /// <summary>Renders a renderer-aware unsaved page preview request.</summary>
    Task<Result<string, AeroError>> RenderPageFragmentAsync(
        PreviewPageFragmentRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Renders an unsaved blog post document to an HTML fragment.
    /// </summary>
    Task<Result<string, AeroError>> RenderBlogPostFragmentAsync(string markdownContent, CancellationToken ct = default);

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
        HtmlPageContent content,
        CancellationToken ct = default)
        => await RenderPageFragmentAsync(content, composition: null, culture: null, ct);

    /// <inheritdoc />
    public async Task<Result<string, AeroError>> RenderPageFragmentAsync(
        HtmlPageContent content,
        PageCompositionDocument? composition,
        string? culture,
        CancellationToken ct = default)
        => await RenderPageFragmentAsync(
            new PreviewPageFragmentRequest(content, composition, culture),
            ct);

    /// <inheritdoc />
    public async Task<Result<string, AeroError>> RenderPageFragmentAsync(
        PreviewPageFragmentRequest request,
        CancellationToken ct = default)
    {
        var result = await PostAsync<PreviewPageFragmentRequest, PreviewPageFragmentResponse>(
            "pages/render-fragment",
            request,
            ct);

        if (result is Result<PreviewPageFragmentResponse, AeroError>.Ok ok)
            return new Result<string, AeroError>.Ok(ok.Value.Html);
        return new Result<string, AeroError>.Failure(
            NormalizePreviewError(
                ((Result<PreviewPageFragmentResponse, AeroError>.Failure)result).Error));
    }

    /// <inheritdoc />
    public async Task<Result<string, AeroError>> RenderBlogPostFragmentAsync(string markdownContent, CancellationToken ct = default)
    {
        var result = await PostAsync<PreviewBlogPostFragmentRequest, PreviewBlogPostFragmentResponse>(
            "blog-posts/render-fragment",
            new PreviewBlogPostFragmentRequest(markdownContent),
            ct);

        if (result is Result<PreviewBlogPostFragmentResponse, AeroError>.Ok ok)
            return new Result<string, AeroError>.Ok(ok.Value.Html);
        return new Result<string, AeroError>.Failure(((Result<PreviewBlogPostFragmentResponse, AeroError>.Failure)result).Error);
    }

    private static AeroError NormalizePreviewError(AeroError error)
    {
        if (error is not AeroError.HttpRequest { msg: { Length: > 0 } body })
            return error;

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            foreach (var propertyName in new[] { "error", "detail", "title" })
            {
                if (root.TryGetProperty(propertyName, out var property)
                    && property.ValueKind == JsonValueKind.String
                    && property.GetString() is { Length: > 0 } message)
                {
                    return AeroError.ValidationError([message]);
                }
            }
        }
        catch (JsonException)
        {
            // The transport may return plain text. Preserve it as the actionable
            // message rather than exposing the HttpRequest record representation.
            return AeroError.ValidationError([body]);
        }

        return error;
    }

}
