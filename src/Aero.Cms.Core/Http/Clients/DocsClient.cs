namespace Aero.Cms.Core.Http.Clients;

using Microsoft.Extensions.Logging;
using Aero.Cms.Core;

public sealed class DocsClient(HttpClient httpClient, ILogger<DocsClient> logger) 
    : AeroClientBase(httpClient, logger)
{
    protected override string ResourceName => "docs";

    public Task<IReadOnlyList<DocsSummary>?> GetAllAsync(CancellationToken ct = default)
        => GetAsync<IReadOnlyList<DocsSummary>>("", ct);

    public Task<DocsDetail?> GetByIdAsync(long id, CancellationToken ct = default)
        => GetAsync<DocsDetail>($"{id}", ct);

    public Task<DocsDetail?> GetBySlugAsync(string slug, CancellationToken ct = default)
        => GetAsync<DocsDetail>($"by-slug/{slug}", ct);

    public Task<IReadOnlyList<DocsSummary>?> GetCategoriesAsync(CancellationToken ct = default)
        => GetAsync<IReadOnlyList<DocsSummary>>("categories", ct);

    public Task<IReadOnlyList<DocsSummary>?> GetChildrenAsync(long parentId, CancellationToken ct = default)
        => GetAsync<IReadOnlyList<DocsSummary>>($"{parentId}/children", ct);

    public Task<DocsDetail?> SaveAsync(DocsDetail page, CancellationToken ct = default)
        => PostAsync<DocsDetail, DocsDetail>("", page, ct);

    public Task<bool> DeleteAsync(long id, CancellationToken ct = default)
        => base.DeleteAsync($"{id}", ct);
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
