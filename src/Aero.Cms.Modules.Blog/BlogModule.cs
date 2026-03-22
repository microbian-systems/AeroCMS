using Aero.Cms.Core;
using Aero.Cms.Core.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.Blog;

public sealed class BlogModule : AeroModuleBase
{
    public override string Name => nameof(BlogModule);
    public override string Version => AeroVersion.Version;
    public override string Author => AeroConstants.Author;
    public override IReadOnlyList<string> Dependencies => [nameof(Aero.Cms.Modules.Pages.PagesModule)];
    public override IReadOnlyList<string> Category => ["content", "blog"];
    public override IReadOnlyList<string> Tags => ["content", "blog", "cms"];

    public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        services.AddScoped<IBlogPostContentService, MartenBlogPostContentService>();
    }

    public override async Task RunAsync(IEndpointRouteBuilder app)
    {
        await base.RunAsync(app);

        var blogService = app.ServiceProvider.GetRequiredService<IBlogPostContentService>();
        app.MapGet("/blog", async (CancellationToken ct) =>
        {
            var posts = await blogService.GetLatestPostsAsync(13, ct);
            var featured = posts.Take(3).ToList();
            var latest = posts.Skip(3).Take(10).ToList();

            var html = BuildBlogListHtml(featured, latest);
            return Results.Text(html, "text/html");
        });

        app.MapGet("/blog/{slug}", async (string slug, CancellationToken ct) =>
        {
            var post = await blogService.FindBySlugAsync(slug, ct);
            return post is not null ? Results.Ok(post.Body) : Results.NotFound();
        });
    }

    private static string BuildBlogListHtml(IReadOnlyList<BlogPostDocument> featured, IReadOnlyList<BlogPostDocument> latest)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html>");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"utf-8\">");
        sb.AppendLine("<title>Blog</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body { font-family: system-ui, sans-serif; max-width: 1200px; margin: 0 auto; padding: 2rem; }");
        sb.AppendLine("h1 { margin-bottom: 2rem; }");
        sb.AppendLine("h2 { margin-top: 2rem; margin-bottom: 1rem; }");
        sb.AppendLine(".featured-grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 1.5rem; margin-bottom: 2rem; }");
        sb.AppendLine(".latest-grid { display: grid; grid-template-columns: repeat(2, 1fr); gap: 1.5rem; }");
        sb.AppendLine(".post-card { border: 1px solid #e5e7eb; border-radius: 8px; padding: 1rem; }");
        sb.AppendLine(".post-card h3 { margin: 0 0 0.5rem 0; }");
        sb.AppendLine(".post-card a { text-decoration: none; color: inherit; }");
        sb.AppendLine(".post-card a:hover h3 { color: #3b82f6; }");
        sb.AppendLine(".post-card p { color: #6b7280; font-size: 0.875rem; margin: 0.5rem 0 0 0; }");
        sb.AppendLine(".post-date { color: #9ca3af; font-size: 0.75rem; }");
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("<h1>Blog</h1>");

        if (featured.Count > 0)
        {
            sb.AppendLine("<h2>Featured</h2>");
            sb.AppendLine("<div class=\"featured-grid\">");
            foreach (var post in featured)
            {
                sb.AppendLine($"<div class=\"post-card\">");
                sb.AppendLine($"<a href=\"/blog/{post.Slug}\">");
                sb.AppendLine($"<h3>{System.Web.HttpUtility.HtmlEncode(post.Title)}</h3>");
                if (!string.IsNullOrEmpty(post.Excerpt))
                {
                    sb.AppendLine($"<p>{System.Web.HttpUtility.HtmlEncode(post.Excerpt)}</p>");
                }
                if (post.PublishedAtUtc.HasValue)
                {
                    sb.AppendLine($"<p class=\"post-date\">{post.PublishedAtUtc.Value:MMMM d, yyyy}</p>");
                }
                sb.AppendLine($"</a>");
                sb.AppendLine($"</div>");
            }
            sb.AppendLine("</div>");
        }

        if (latest.Count > 0)
        {
            sb.AppendLine("<h2>Latest</h2>");
            sb.AppendLine("<div class=\"latest-grid\">");
            foreach (var post in latest)
            {
                sb.AppendLine($"<div class=\"post-card\">");
                sb.AppendLine($"<a href=\"/blog/{post.Slug}\">");
                sb.AppendLine($"<h3>{System.Web.HttpUtility.HtmlEncode(post.Title)}</h3>");
                if (!string.IsNullOrEmpty(post.Excerpt))
                {
                    sb.AppendLine($"<p>{System.Web.HttpUtility.HtmlEncode(post.Excerpt)}</p>");
                }
                if (post.PublishedAtUtc.HasValue)
                {
                    sb.AppendLine($"<p class=\"post-date\">{post.PublishedAtUtc.Value:MMMM d, yyyy}</p>");
                }
                sb.AppendLine($"</a>");
                sb.AppendLine($"</div>");
            }
            sb.AppendLine("</div>");
        }

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }
}
