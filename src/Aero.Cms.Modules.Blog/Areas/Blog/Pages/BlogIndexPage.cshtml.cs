using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Aero.Cms.Core;
using Aero.Cms.Modules.Blog.Models;
using Marten;
using Marten.Pagination;

namespace Aero.Cms.Modules.Blog.Areas.Blog.Pages;

public class BlogIndexPageModel(IBlogPostContentService blogService) : PageModel
{
    public int PageNumber { get; private set; } = 1;
    public int PageSize { get; private set; } = 9; 
    public bool HasMore { get; private set; }
    public IReadOnlyList<BlogPostDocument> Posts { get; private set; } = [];
    public IReadOnlyList<BlogPostDocument> FeaturedPosts { get; private set; } = [];
    public IReadOnlyList<BlogPostDocument> OtherPosts { get; private set; } = [];
    public Dictionary<long, string> TagNames { get; private set; } = [];

    public async Task OnGetAsync(int? p, CancellationToken cancellationToken = default)
    {
        PageNumber = p ?? 1;
        
        var result = await blogService.GetPagedPostsAsync(PageNumber, PageSize, cancellationToken);
        var tagsResult = await blogService.GetAllTagsAsync(cancellationToken);

        TagNames = tagsResult switch
        {
            Result<string, IReadOnlyList<Tag>>.Ok(var tags) => tags.ToDictionary(t => t.Id, t => t.Name),
            _ => []
        };

        if (result is global::Aero.Core.Railway.Result<string, global::Marten.Pagination.IPagedList<BlogPostDocument>>.Ok(var pagedList))
        {
            Posts = pagedList.ToList();
            HasMore = pagedList.HasNextPage;

            if (PageNumber == 1)
            {
                FeaturedPosts = Posts.Take(3).ToList();
                OtherPosts = Posts.Skip(3).ToList();
            }
            else
            {
                OtherPosts = Posts;
            }
        }
    }

    public async Task<IActionResult> OnGetLoadMoreAsync(int p, CancellationToken cancellationToken = default)
    {
        PageNumber = p;
        var result = await blogService.GetPagedPostsAsync(PageNumber, PageSize, cancellationToken);
        var tagsResult = await blogService.GetAllTagsAsync(cancellationToken);

        TagNames = tagsResult switch
        {
            Result<string, IReadOnlyList<Tag>>.Ok(var tags) => tags.ToDictionary(t => t.Id, t => t.Name),
            _ => []
        };

        if (result is global::Aero.Core.Railway.Result<string, global::Marten.Pagination.IPagedList<BlogPostDocument>>.Ok(var pagedList))
        {
            OtherPosts = pagedList.ToList();
            HasMore = pagedList.HasNextPage;
            return Partial("_BlogPostsList", this);
        }

        return NotFound();
    }
}