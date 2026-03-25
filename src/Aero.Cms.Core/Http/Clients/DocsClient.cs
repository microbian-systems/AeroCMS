using Aero.Core.Railway;
using Microsoft.Extensions.Logging;
using Aero.Cms.Core;

namespace Aero.Cms.Core.Http.Clients;

public sealed class DocsClient(HttpClient httpClient, ILogger<DocsClient> logger) 
    : AeroCmsClientBase(httpClient, logger)
{
    protected override string ResourceName => "docs";

    public Task<Result<string, IReadOnlyList<DocsSummary>>> GetAllAsync(CancellationToken ct = default)
        => GetResultAsync<IReadOnlyList<DocsSummary>>("", ct);

    public Task<Result<string, DocsDetail>> GetByIdAsync(long id, CancellationToken ct = default)
        => GetResultAsync<DocsDetail>($"{id}", ct);

    public Task<Result<string, DocsDetail>> GetBySlugAsync(string slug, CancellationToken ct = default)
        => GetResultAsync<DocsDetail>($"by-slug/{slug}", ct);

    public Task<Result<string, IReadOnlyList<DocsSummary>>> GetCategoriesAsync(CancellationToken ct = default)
        => GetResultAsync<IReadOnlyList<DocsSummary>>("categories", ct);

    public Task<Result<string, IReadOnlyList<DocsSummary>>> GetChildrenAsync(long parentId, CancellationToken ct = default)
        => GetResultAsync<IReadOnlyList<DocsSummary>>($"{parentId}/children", ct);

    public Task<Result<string, DocsDetail>> SaveAsync(DocsDetail page, CancellationToken ct = default)
        => PostResultAsync<DocsDetail, DocsDetail>("", page, ct);

    public Task<Result<string, bool>> DeleteAsync(long id, CancellationToken ct = default)
        => base.DeleteResultAsync($"{id}", ct);
}

public record DocsSummary(long Id, string Title, string Slug, long? ParentId, int Order);
public record DocsDetail(
    long Id, 
    string Title, 
    string Slug, 
    string? Summary, 
    string? MarkdownContent, 
    long? ParentId, 
    int Order,
    ContentPublicationState PublicationState);
