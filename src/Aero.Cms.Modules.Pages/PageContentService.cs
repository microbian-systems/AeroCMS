using Aero.Cms.Core.Blocks;
using Aero.Cms.Core.Blocks.Common;
using Aero.Cms.Core.Blocks.Layout;
using Aero.Cms.Modules.Pages.Validators;
using Aero.Core;
using Aero.Core.Extensions;
using Wolverine;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Events;
using Aero.Cms.Core.Entities;


namespace Aero.Cms.Modules.Pages;

public interface IPageContentService
{
    Task<Result<string, PageDocument?>> LoadAsync(long id, CancellationToken cancellationToken = default);
    Task<Result<string, PageDocument?>> FindBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<Result<string, PageDocument?>> LoadHomepageAsync(CancellationToken cancellationToken = default);
    Task<Result<string, PageDocument?>> LoadBlogListingAsync(CancellationToken cancellationToken = default);
    Task<Result<string, (IReadOnlyList<PageDocument> Items, long TotalCount)>> GetAllPagesAsync(int skip = 0, int take = 10, string? search = null, CancellationToken cancellationToken = default);
    Task<Result<string, PageDocument>> SaveAsync(PageDocument page, CancellationToken cancellationToken = default);
    Task<Result<string, PageDocument>> CreateAsync(Requests.CreatePageRequest request, CancellationToken cancellationToken = default);
    Task<Result<string, PageDocument>> UpdateAsync(long id, Requests.UpdatePageRequest request, CancellationToken cancellationToken = default);
    Task<Result<string, bool>> DeleteAsync(long id, CancellationToken cancellationToken = default);
}

public sealed class MartenPageContentService(IDocumentSession session, IBlockService blockService, IMessageBus bus) : IPageContentService
{
    public async Task<Result<string, PageDocument?>> LoadAsync(long id, CancellationToken cancellationToken = default)
    {
        try
        {
            var document = await session.LoadAsync<PageDocument>(id, cancellationToken);
            return document is null
                ? Prelude.Fail<string, PageDocument?>($"Page with id '{id}' not found")
                : Prelude.Ok<string, PageDocument?>(document);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<string, PageDocument?>(ex.Message);
        }
    }

    public Task<Result<string, PageDocument?>> LoadHomepageAsync(CancellationToken cancellationToken = default)
        => FindBySlugAsync("/", cancellationToken);

    public Task<Result<string, PageDocument?>> LoadBlogListingAsync(CancellationToken cancellationToken = default)
        => FindBySlugAsync("blog", cancellationToken);

    public async Task<Result<string, (IReadOnlyList<PageDocument> Items, long TotalCount)>> GetAllPagesAsync(int skip = 0, int take = 10, string? search = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = session.Query<PageDocument>();

            IQueryable<PageDocument> filteredQuery = query;
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                filteredQuery = query.Where(x => x.Title.ToLower().Contains(s) || x.Slug.ToLower().Contains(s));
            }
            var stats = new global::Marten.Linq.QueryStatistics();
            var pages = await ((global::Marten.Linq.IMartenQueryable<PageDocument>)filteredQuery)
                .OrderBy(x => x.Title)
                .Stats(out stats)
                .Skip(skip)
                .Take(take)
                .ToListAsync(token: cancellationToken);

            return Prelude.Ok<string, (IReadOnlyList<PageDocument> Items, long TotalCount)>((pages, stats.TotalResults));
        }
        catch (Exception ex)
        {
            return Prelude.Fail<string, (IReadOnlyList<PageDocument> Items, long TotalCount)>(ex.Message);
        }
    }

    public async Task<Result<string, PageDocument?>> FindBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        try
        {
            var reservation = await session.Query<ContentSlugDocument>()
                .FirstOrDefaultAsync(x =>
                    string.Equals(slug, x.Slug, StringComparison.CurrentCultureIgnoreCase), token: cancellationToken);
            if (reservation is null || reservation.OwnerType != ContentSlugOwnerType.Page)
            {
                return Prelude.Fail<string, PageDocument?>($"Page with slug '{slug}' not found");
            }

            var document = await session.LoadAsync<PageDocument>(reservation.OwnerId, cancellationToken);
            return document is null
                ? Prelude.Fail<string, PageDocument?>($"Page with id '{reservation.OwnerId}' not found")
                : Prelude.Ok<string, PageDocument?>(document);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<string, PageDocument?>(ex.Message);
        }
    }

    public async Task<Result<string, PageDocument>> CreateAsync(Requests.CreatePageRequest request, CancellationToken cancellationToken = default)
    {
        var page = new PageDocument
        {
            Id = Snowflake.NewId(),
            Title = request.Title,
            Slug = string.IsNullOrEmpty(request.Slug)
                ? request.Title.GenerateSlug()
                : request.Slug,
            Summary = request.Summary,
            SeoTitle = request.SeoTitle,
            SeoDescription = request.SeoDescription,
            PublicationState = request.PublicationState,
            ShowInNavMenu = request.ShowInNavMenu
        };

        if (request.EditorBlocks is { Count: > 0 })
        {
            page.Blocks = request.EditorBlocks.ToList();
            page.LayoutRegions = await MapEditorBlocksToLayoutRegions(request.EditorBlocks, cancellationToken);
        }
        else
        {
            page.Blocks = new List<EditorBlock>();
            page.LayoutRegions = request.LayoutRegions?.ToList() ?? [];
        }

        return await SaveAsync(page, cancellationToken);
    }

    public async Task<Result<string, PageDocument>> UpdateAsync(long id, Requests.UpdatePageRequest request, CancellationToken cancellationToken = default)
    {
        var loadResult = await LoadAsync(id, cancellationToken);
        if (loadResult is Result<string, PageDocument?>.Ok { Value: not null } ok)
        {
            var page = ok.Value;
            page.Title = request.Title;
            page.Slug = request.Slug;
            page.Summary = request.Summary;
            page.SeoTitle = request.SeoTitle;
            page.SeoDescription = request.SeoDescription;
            page.PublicationState = request.PublicationState;
            if (request.EditorBlocks is { Count: > 0 })
            {
                page.Blocks = request.EditorBlocks.ToList();
                page.LayoutRegions = await MapEditorBlocksToLayoutRegions(request.EditorBlocks, cancellationToken);
            }
            else
            {
                page.Blocks = new List<EditorBlock>();
                page.LayoutRegions = request.LayoutRegions?.ToList() ?? [];
            }

            return await SaveAsync(page, cancellationToken);
        }

        return Prelude.Fail<string, PageDocument>($"Page with id '{id}' not found");
    }

    public async Task<Result<string, bool>> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        try
        {
            var reservation = await session.Query<ContentSlugDocument>()
                .FirstOrDefaultAsync(x => x.OwnerId == id && x.OwnerType == ContentSlugOwnerType.Page, token: cancellationToken);

            if (reservation is not null)
            {
                session.Delete(reservation);
            }

            session.Delete<PageDocument>(id);
            await session.SaveChangesAsync(cancellationToken);
            return Prelude.Ok<string, bool>(true);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<string, bool>(ex.Message);
        }
    }

    public async Task<Result<string, PageDocument>> SaveAsync(PageDocument page, CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(page);
            await ValidatePage(page);

            var existingPage = await session.LoadAsync<PageDocument>(page.Id, cancellationToken);
            await ContentSlugReservation.ReserveAsync(
                session,
                page.Id,
                ContentSlugOwnerType.Page,
                page.Slug,
                existingPage?.Slug,
                cancellationToken);

            var now = DateTimeOffset.UtcNow;
            var existingCreatedOn = existingPage?.CreatedOn;
            page.CreatedOn = existingCreatedOn is null || existingCreatedOn == default ? now : existingCreatedOn.Value;
            page.ModifiedOn = now;
            page.PublishedOn = page.PublicationState == ContentPublicationState.Published
                ? existingPage?.PublishedOn ?? now
                : null;

            session.Store(page);
            await session.SaveChangesAsync(cancellationToken);

            if (page.PublicationState == ContentPublicationState.Published)
            {
                await bus.PublishAsync(new SlugUpdated(page.Id, "Page", page.Slug, existingPage?.Slug));
            }

            return Prelude.Ok<string, PageDocument>(page);

        }
        catch (ArgumentException ex)
        {
            return Prelude.Fail<string, PageDocument>(ex.Message);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<string, PageDocument>(ex.Message);
        }
    }

    private async Task<List<LayoutRegion>> MapEditorBlocksToLayoutRegions(IReadOnlyList<EditorBlock> editorBlocks, CancellationToken cancellationToken)
    {
        var placements = new List<BlockPlacement>();
        int order = 0;

        foreach (var eb in editorBlocks)
        {
            var block = MapEditorBlock(eb);
            if (block != null)
            {
                await blockService.SaveAsync(block, cancellationToken);
                placements.Add(new BlockPlacement
                {
                    BlockId = block.Id,
                    Order = order++
                });
            }
        }

        // For now, put all editor blocks in a single column in one "Main" region
        var column = new LayoutColumn
        {
            Width = 12, // full width
            Blocks = placements
        };

        return [
            new LayoutRegion
            {
                Name = "Main",
                Order = 0,
                Columns = [column]
            }
        ];
    }

    private BlockBase? MapEditorBlock(EditorBlock eb)
    {
        return eb.Type switch
        {
            "aero_hero" => new AeroHeroBlock
            {
                Title = eb.MainText,
                Description = eb.SubText,
                BackgroundImage = eb.BackgroundImage,
                Layout = Enum.TryParse<AeroHeroLayout>(eb.AeroLayout, true, out var layout) ? layout : AeroHeroLayout.SideImage,
                Buttons = new List<AeroButton>
                {
                    new AeroButton { Text = eb.CtaText, Url = eb.CtaUrl, Style = AeroButtonStyle.Primary },
                    new AeroButton { Text = eb.CtaText2, Url = eb.CtaUrl2, Style = AeroButtonStyle.Secondary }
                }
            },
            "aero_features" => new AeroFeaturesBlock
            {
                Title = eb.MainText,
                SubTitle = eb.SubText,
                Layout = Enum.TryParse<AeroFeaturesLayout>(eb.AeroLayout, true, out var layout) ? layout : AeroFeaturesLayout.Simple,
                Items = eb.FeatureItems.Select(f => new AeroFeatureItem
                {
                    Title = f.Title,
                    Description = f.Description,
                    Icon = f.Icon,
                    ImageUrl = f.ImageUrl,
                    LinkUrl = f.LinkUrl
                }).ToList()
            },
            "aero_cta" => new AeroCtaBlock
            {
                Title = eb.MainText,
                Description = eb.SubText,
                CtaText = eb.CtaText,
                CtaUrl = eb.CtaUrl,
                Layout = Enum.TryParse<AeroCtaLayout>(eb.AeroLayout, true, out var layout) ? layout : AeroCtaLayout.Card
            },
            "aero_blog" => new AeroBlogBlock
            {
                Title = eb.MainText,
                Description = eb.SubText,
                Posts = eb.BlogPosts.Select(p => new AeroBlogItem
                {
                    Title = p.Title,
                    Description = p.Description,
                    ImageUrl = p.ImageUrl,
                    AuthorName = p.AuthorName,
                    PublishedAt = p.PublishedAt,
                    Category = p.Category,
                    PostUrl = p.PostUrl
                }).ToList()
            },
            "aero_pricing" => new AeroPricingBlock
            {
                Title = eb.MainText,
                Description = eb.SubText,
                Plans = eb.PricingPlans.Select(p => new AeroPricingPlan
                {
                    Name = p.Name,
                    Price = p.Price,
                    Period = p.Period,
                    Description = p.Description,
                    Features = p.Features,
                    CtaText = p.CtaText,
                    CtaUrl = p.CtaUrl,
                    IsPopular = p.IsPopular
                }).ToList()
            },
            "aero_teams" => new AeroTeamsBlock
            {
                Title = eb.MainText,
                Description = eb.SubText,
                Members = eb.TeamMembers.Select(m => new AeroTeamMember
                {
                    Name = m.Name,
                    Role = m.Role,
                    AvatarUrl = m.AvatarUrl,
                    Description = m.Description,
                    LinkedInUrl = m.LinkedInUrl
                }).ToList()
            },
            "aero_testimonials" => new AeroTestimonialsBlock
            {
                Title = eb.MainText,
                Description = eb.SubText,
                Testimonials = eb.Testimonials.Select(t => new AeroTestimonialItem
                {
                    AuthorName = t.AuthorName,
                    AuthorRole = t.AuthorRole,
                    AuthorImage = t.AuthorImage,
                    Content = t.Content,
                    StarRating = t.StarRating,
                    CompanyName = t.CompanyName
                }).ToList()
            },
            "aero_faq" => new AeroFaqBlock
            {
                Title = eb.MainText,
                Description = eb.SubText,
                Items = eb.FaqItems.Select(f => new AeroFaqItem
                {
                    Question = f.Question,
                    Answer = f.Answer
                }).ToList()
            },
            "raw_html" => new RawHtmlBlock
            {
                Content = eb.Content
            },
            "rich_text" => new RichTextBlock
            {
                Content = eb.Content
            },
            "heading" => new HeadingBlock
            {
                Text = eb.Title,
                Level = 2 // default
            },
            "quote" => new QuoteBlock
            {
                Content = eb.Content,
                Author = eb.Author
            },
            "image" => new ImageBlock
            {
                AltText = eb.Alt,
                Caption = eb.Caption,
                // If it's a URL, we might not have a MediaId, but ImageBlock currently only has MediaId.
                // We'll leave it as 0 for now or if we had a way to map URL to ID.
            },
            "video" => new EmbedBlock
            {
                SourceUrl = eb.Url,
                EmbedType = "video"
            },
            "gallery" => new CarouselBlock
            {
                Items = eb.GalleryImages.Select(g => new CarouselItem
                {
                    AltText = g.Alt,
                    Caption = g.Src // using Src as caption for now if needed, or mapping correctly
                }).ToList()
            },
            _ => null
        };
    }

    private static async Task ValidatePage(PageDocument page)
    {
        var validator = new PageDocumentValidator();
        var valid = await validator.ValidateAsync(page);

        if (valid.Errors.Any())
        {
            throw new ArgumentException($"page errors: {string.Join(", ", valid.Errors.Select(e => e.ErrorMessage))}");
        }
    }
}
