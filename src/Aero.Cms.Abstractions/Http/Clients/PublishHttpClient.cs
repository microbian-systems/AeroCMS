namespace Aero.Cms.Abstractions.Http.Clients;

using Aero.Cms.Abstractions.Enums;
using Aero.Core.Railway;
using Microsoft.Extensions.Logging;

/// <summary>
/// Interface for publish HTTP client.
/// </summary>
public interface IPublishHttpClient
{
    /// <summary>
    /// Publishes a page by its identifier.
    /// </summary>
    /// <param name="id">The page identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The publish response or an error.</returns>
    Task<Result<PublishResponse, AeroError>> PublishPageAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// Publishes a blog post by its identifier.
    /// </summary>
    /// <param name="id">The blog post identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The publish response or an error.</returns>
    Task<Result<PublishResponse, AeroError>> PublishBlogPostAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// Unpublishes a page by its identifier.
    /// </summary>
    /// <param name="id">The page identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The publish response or an error.</returns>
    Task<Result<PublishResponse, AeroError>> UnpublishPageAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// Unpublishes a blog post by its identifier.
    /// </summary>
    /// <param name="id">The blog post identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The publish response or an error.</returns>
    Task<Result<PublishResponse, AeroError>> UnpublishBlogPostAsync(long id, CancellationToken ct = default);
}

/// <summary>
/// Typed client for publish endpoints.
/// </summary>
public class PublishHttpClient(HttpClient httpClient, ILogger<PublishHttpClient> logger) 
    : AeroCmsClientBase(httpClient, logger), IPublishHttpClient
{
    /// <inheritdoc />
    public override string Path => "admin/publish";

    /// <inheritdoc />
    public Task<Result<PublishResponse, AeroError>> PublishPageAsync(long id, CancellationToken ct = default)
    {
        return PostAsync<object, PublishResponse>($"pages/{id}", new object(), ct);
    }

    /// <inheritdoc />
    public Task<Result<PublishResponse, AeroError>> PublishBlogPostAsync(long id, CancellationToken ct = default)
    {
        return PostAsync<object, PublishResponse>($"blog-posts/{id}", new object(), ct);
    }

    /// <inheritdoc />
    public Task<Result<PublishResponse, AeroError>> UnpublishPageAsync(long id, CancellationToken ct = default)
    {
        // Using relative path to access unpublish endpoint if it's outside the standard 'publish' prefix.
        // Assuming the current ResourceName is 'publish', this becomes 'unpublish/pages/{id}'
        return PostAsync<object, PublishResponse>("../unpublish/pages/" + id, new object(), ct);
    }

    /// <inheritdoc />
    public Task<Result<PublishResponse, AeroError>> UnpublishBlogPostAsync(long id, CancellationToken ct = default)
    {
        return PostAsync<object, PublishResponse>("../unpublish/blog-posts/" + id, new object(), ct);
    }
}

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

/// <summary>
/// Response after a publication operation.
/// </summary>
/// <param name="Id">The content identifier.</param>
/// <param name="ContentType">The content type.</param>
/// <param name="PublicationState">The current publication state.</param>
/// <param name="PublishedOn">The publication date/time.</param>
public record PublishResponse(long Id, string ContentType, ContentPublicationState PublicationState, DateTimeOffset? PublishedOn);
