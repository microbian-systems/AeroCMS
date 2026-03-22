using Aero.Cms.Core.Blocks;
using Aero.Cms.Modules.Blog;
using Aero.Cms.Modules.Pages;
using Aero.Cms.Modules.Pages.Models;
using Aero.Core;
using FlakeId;
using Marten;

namespace Aero.Cms.Modules.Setup;

public sealed record SetupCompletionRequest(
    string AdminUserName,
    string AdminEmail,
    string Password,
    string SiteName,
    string HomepageTitle,
    string BlogName);

public sealed class SetupCompletionResult
{
    public bool Succeeded => Errors.Count == 0;
    public bool AlreadyComplete { get; init; }
    public bool CreatedAdmin { get; init; }
    public bool CreatedRoles { get; init; }
    public List<string> Errors { get; } = [];

    public static SetupCompletionResult Failure(params string[] errors)
        => Failure(errors.AsEnumerable());

    public static SetupCompletionResult Failure(IEnumerable<string> errors)
    {
        var result = new SetupCompletionResult();
        result.Errors.AddRange(errors.Where(error => !string.IsNullOrWhiteSpace(error)));
        return result;
    }
}

public interface ISetupCompletionService
{
    Task<SetupCompletionResult> CompleteAsync(SetupCompletionRequest request, CancellationToken cancellationToken = default);
}

public sealed class SetupCompletionService(
    IDocumentSession session,
    ISetupIdentityBootstrapper identityBootstrapper,
    IPageContentService pageContentService,
    IBlogPostContentService blogPostContentService) : ISetupCompletionService
{
    public async Task<SetupCompletionResult> CompleteAsync(SetupCompletionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var existingState = await session.LoadAsync<SetupStateDocument>(SetupStateDocument.FixedId, cancellationToken);
        if (existingState?.IsComplete == true)
        {
            return new SetupCompletionResult
            {
                AlreadyComplete = true
            };
        }

        var identityResult = await identityBootstrapper.BootstrapAsync(
            new SetupIdentityBootstrapRequest(
                request.AdminUserName,
                request.AdminEmail,
                request.Password),
            cancellationToken);

        if (!identityResult.Succeeded)
        {
            return SetupCompletionResult.Failure(identityResult.Errors.Select(error => error.Description));
        }

        try
        {
            await SeedStarterContentAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            return SetupCompletionResult.Failure(ex.Message);
        }

        var completedAtUtc = existingState?.CompletedAtUtc ?? DateTimeOffset.UtcNow;
        session.Store(new SetupStateDocument
        {
            Id = SetupStateDocument.FixedId,
            IsComplete = true,
            CompletedAtUtc = completedAtUtc
        });
        await session.SaveChangesAsync(cancellationToken);

        return new SetupCompletionResult
        {
            CreatedAdmin = identityResult.CreatedAdmin,
            CreatedRoles = identityResult.CreatedRoles
        };
    }

    private async Task SeedStarterContentAsync(SetupCompletionRequest request, CancellationToken cancellationToken)
    {
        // Build homepage and store its blocks first
        var (homepage, homepageBlocks) = BuildHomepage(request);
        foreach (var block in homepageBlocks)
        {
            session.Store(block);
        }
        await pageContentService.SaveAsync(homepage, cancellationToken);

        // Build blog listing page and store its blocks first
        var (blogListing, blogListingBlocks) = BuildBlogListingPage(request);
        foreach (var block in blogListingBlocks)
        {
            session.Store(block);
        }
        await pageContentService.SaveAsync(blogListing, cancellationToken);

        // Save blog posts (blocks are stored inline in Content)
        foreach (var post in BuildStarterBlogPosts(request))
        {
            await blogPostContentService.SaveAsync(post, cancellationToken);
        }
    }

    private static (PageDocument Page, List<BlockBase> Blocks) BuildHomepage(SetupCompletionRequest request)
    {
        var headingBlock = new HeadingBlock
        {
            Id = Snowflake.NewId(),
            Level = 1,
            Text = Normalize(request.HomepageTitle),
            Order = 0
        };
        var bodyBlock = new RichTextBlock
        {
            Id = Snowflake.NewId(),
            Content = $"<p>{Normalize(request.SiteName)} is now configured. Visit the blog to publish your next update.</p>",
            Order = 1
        };

        return (
            new PageDocument
            {
                Id = PageDocumentIds.Homepage,
                Kind = PageKind.Homepage,
                Slug = "/",
                Title = Normalize(request.HomepageTitle),
                Summary = $"{Normalize(request.SiteName)} is ready with a published homepage, blog, and starter stories.",
                SeoTitle = $"{Normalize(request.HomepageTitle)} | {Normalize(request.SiteName)}",
                SeoDescription = $"Launch {Normalize(request.SiteName)} with a published homepage and starter content.",
                LayoutRegions =
                [
                    new LayoutRegion
                    {
                        Name = "MainContent",
                        Order = 0,
                        Columns =
                        [
                            new LayoutColumn
                            {
                                Width = 12,
                                Order = 0,
                                Blocks =
                                [
                                    new BlockPlacement { BlockId = headingBlock.Id, BlockType = headingBlock.BlockType, Order = 0 },
                                    new BlockPlacement { BlockId = bodyBlock.Id, BlockType = bodyBlock.BlockType, Order = 1 }
                                ]
                            }
                        ]
                    }
                ],
                PublicationState = ContentPublicationState.Published
            },
            new List<BlockBase> { headingBlock, bodyBlock }
        );
    }

    private static (PageDocument Page, List<BlockBase> Blocks) BuildBlogListingPage(SetupCompletionRequest request)
    {
        var headingBlock = new HeadingBlock
        {
            Id = Snowflake.NewId(),
            Level = 1,
            Text = Normalize(request.BlogName),
            Order = 0
        };
        var bodyBlock = new RichTextBlock
        {
            Id = Snowflake.NewId(),
            Content = "<p>Three example posts are already published so the site is usable right away.</p>",
            Order = 1
        };

        return (
            new PageDocument
            {
                Id = PageDocumentIds.BlogListing,
                Kind = PageKind.BlogListing,
                Slug = "blog",
                Title = Normalize(request.BlogName),
                Summary = $"Updates and field notes from {Normalize(request.SiteName)}.",
                SeoTitle = $"{Normalize(request.BlogName)} | {Normalize(request.SiteName)}",
                SeoDescription = $"Read the latest posts from {Normalize(request.SiteName)}.",
                LayoutRegions =
                [
                    new LayoutRegion
                    {
                        Name = "MainContent",
                        Order = 0,
                        Columns =
                        [
                            new LayoutColumn
                            {
                                Width = 12,
                                Order = 0,
                                Blocks =
                                [
                                    new BlockPlacement { BlockId = headingBlock.Id, BlockType = headingBlock.BlockType, Order = 0 },
                                    new BlockPlacement { BlockId = bodyBlock.Id, BlockType = bodyBlock.BlockType, Order = 1 }
                                ]
                            }
                        ]
                    }
                ],
                PublicationState = ContentPublicationState.Published
            },
            new List<BlockBase> { headingBlock, bodyBlock }
        );
    }

    private static IReadOnlyList<BlogPostDocument> BuildStarterBlogPosts(SetupCompletionRequest request)
        =>
        [
            CreatePost(
                id: Snowflake.NewId(),
                slug: "blog/getting-started-with-aero-cms",
                title: "Getting Started with Aero CMS",
                excerpt: $"Use {Normalize(request.SiteName)} to publish your first update in minutes.",
                headingText: "Getting Started with Aero CMS",
                bodyHtml: $"<p>Your site is live with a homepage and blog. Use this starter post as the baseline for your first editorial update in {Normalize(request.SiteName)}.</p>"),
            CreatePost(
                id: Snowflake.NewId(),
                slug: "blog/shaping-your-homepage-message",
                title: "Shaping Your Homepage Message",
                excerpt: "Clarify what visitors should understand in the first screenful.",
                headingText: "Shaping Your Homepage Message",
                bodyHtml: $"<p>Start with the promise behind {Normalize(request.HomepageTitle)} and keep the lead paragraph focused on the outcome your site delivers.</p>"),
            CreatePost(
                id: Snowflake.NewId(),
                slug: "blog/publishing-your-first-update",
                title: "Publishing Your First Update",
                excerpt: $"Turn {Normalize(request.BlogName)} into a steady publishing habit.",
                headingText: "Publishing Your First Update",
                bodyHtml: $"<p>Add a new story to {Normalize(request.BlogName)} as soon as setup finishes so the starter content becomes your real editorial cadence.</p>")
        ];

    private static BlogPostDocument CreatePost(long id, string slug, string title, string excerpt, string headingText, string bodyHtml)
        => new()
        {
            Id = id,
            Slug = slug,
            Title = title,
            Excerpt = excerpt,
            SeoTitle = title,
            SeoDescription = excerpt,
            Content =
            [
                new HeadingBlock { Id = Snowflake.NewId(), Level = 1, Text = headingText, Order = 0 },
                new RichTextBlock { Id = Snowflake.NewId(), Content = bodyHtml, Order = 1 }
            ],
            PublicationState = ContentPublicationState.Published
        };

    private static string Normalize(string value)
        => value.Trim();
}
