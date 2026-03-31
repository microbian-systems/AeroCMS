namespace Aero.Cms.Core.Http.Clients;

using Aero.Cms.Abstractions.Enums;
using Aero.Core.Railway;
using Microsoft.Extensions.Logging;

public interface IPublishHttpClient
{
    Task<Result<string, PublishResponse>> PublishPageAsync(long id, CancellationToken ct = default);
    Task<Result<string, PublishResponse>> PublishBlogPostAsync(long id, CancellationToken ct = default);
    Task<Result<string, PublishResponse>> UnpublishPageAsync(long id, CancellationToken ct = default);
    Task<Result<string, PublishResponse>> UnpublishBlogPostAsync(long id, CancellationToken ct = default);
}

public class PublishHttpClient(HttpClient httpClient, ILogger<PublishHttpClient> logger) 
    : AeroCmsClientBase(httpClient, logger), IPublishHttpClient
{
    protected override string ResourceName => "publish";

    public Task<Result<string, PublishResponse>> PublishPageAsync(long id, CancellationToken ct = default)
    {
        return PostResultAsync<object, PublishResponse>($"pages/{id}", new object(), ct);
    }

    public Task<Result<string, PublishResponse>> PublishBlogPostAsync(long id, CancellationToken ct = default)
    {
        return PostResultAsync<object, PublishResponse>($"blog-posts/{id}", new object(), ct);
    }

    public Task<Result<string, PublishResponse>> UnpublishPageAsync(long id, CancellationToken ct = default)
    {
        return PostResultAsync<object, PublishResponse>("../unpublish/pages/" + id, new object(), ct);
    }

    public Task<Result<string, PublishResponse>> UnpublishBlogPostAsync(long id, CancellationToken ct = default)
    {
        return PostResultAsync<object, PublishResponse>("../unpublish/blog-posts/" + id, new object(), ct);
    }
}

public record PublishResponse(long Id, string ContentType, ContentPublicationState PublicationState, DateTimeOffset? PublishedOn);
