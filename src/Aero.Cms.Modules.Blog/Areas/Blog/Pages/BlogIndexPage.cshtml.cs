using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Aero.Cms.Core;
using Aero.Cms.Modules.Blog.Models;

namespace Aero.Cms.Modules.Blog.Areas.Blog.Pages;

public class BlogIndexPageModel(IBlogPostContentService blogService) : PageModel
{
    public int PageNumber { get; private set; } = 1;
    public int PageSize { get; private set; } = 10;
    public int TotalCount { get; private set; }
    public int TotalPages { get; private set; }
    public bool HasNextPage { get; private set; }
    public bool HasPreviousPage { get; private set; }

    public IReadOnlyList<BlogPostDocument> FeaturedPosts { get; private set; } = [];
    public IReadOnlyList<BlogPostDocument> OtherPosts { get; private set; } = [];
    public Dictionary<long, string> TagNames { get; private set; } = [];

    public async Task OnGetAsync(int? p, CancellationToken cancellationToken = default)
    {
        PageNumber = p ?? 1;

        // Fetch featured posts (only if we're on page 1 or you want them always)
        // User says "keep them @ top" and "htmx refresh should not affect them"
        // This implies they are rendered once.
        var latestResult = await blogService.GetLatestPostsAsync(3, cancellationToken);
        FeaturedPosts = latestResult switch
        {
            Result<string, IReadOnlyList<BlogPostDocument>>.Ok(var list) => list,
            _ => []
        };

        // Fetch first page of archive
        await LoadOtherPostsAsync(PageNumber, cancellationToken);

        var tagsResult = await blogService.GetAllTagsAsync(cancellationToken);
        TagNames = tagsResult switch
        {
            Result<string, IReadOnlyList<Tag>>.Ok(var tags) => tags.ToDictionary(t => t.Id, t => t.Name),
            _ => []
        };
    }

    public async Task<IActionResult> OnGetPostsPageAsync(int p, CancellationToken cancellationToken = default)
    {
        PageNumber = p;
        await LoadOtherPostsAsync(PageNumber, cancellationToken);

        var tagsResult = await blogService.GetAllTagsAsync(cancellationToken);
        TagNames = tagsResult switch
        {
            Result<string, IReadOnlyList<Tag>>.Ok(var tags) => tags.ToDictionary(t => t.Id, t => t.Name),
            _ => []
        };

        return Partial("_BlogPostsList", this);
    }

    private async Task LoadOtherPostsAsync(int pageNumber, CancellationToken cancellationToken)
    {
        // Fetch archive posts skipping the 3 featured ones
        var result = await blogService.GetPagedPostsAsync(pageNumber, PageSize, skip: 3, cancellationToken);
        
        if (result is Result<string, global::Marten.Pagination.IPagedList<BlogPostDocument>>.Ok(var pagedList))
        {
            OtherPosts = pagedList.ToList();
            TotalCount = (int)pagedList.TotalItemCount; // Note: Marten total count for .Skip(3) might reflect the filtered set or original.
            // Marten's IPagedList.TotalItemCount usually reflects the count of the query *before* paging, but after filtering. 
            // In our case, the skip(3) is part of the query.
            TotalPages = (int)pagedList.PageCount;
            HasNextPage = pagedList.HasNextPage;
            HasPreviousPage = pagedList.HasPreviousPage;
        }
    }
}
