using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Common;
using System.Text.Json;

namespace Aero.Cms.Modules.Pages;

public static class EditorBlockMapper
{
    public static List<BlockBase> MapBlocks(IReadOnlyList<EditorBlock> editorBlocks)
    {
        ArgumentNullException.ThrowIfNull(editorBlocks);

        return editorBlocks
            .Select(MapBlock)
            .OfType<BlockBase>()
            .ToList();
    }

    public static BlockBase? MapBlock(EditorBlock editorBlock)
    {
        ArgumentNullException.ThrowIfNull(editorBlock);

        return editorBlock.Type switch
        {
            "aero_hero" => new AeroHeroBlock
            {
                Title = editorBlock.MainText,
                Description = editorBlock.SubText,
                BackgroundImage = editorBlock.BackgroundImage,
                Layout = Enum.TryParse<AeroHeroLayout>(editorBlock.AeroLayout, true, out var layout)
                    ? layout
                    : AeroHeroLayout.SideImage,
                Buttons =
                [
                    new AeroButton { Text = editorBlock.CtaText, Url = editorBlock.CtaUrl, Style = AeroButtonStyle.Primary },
                    new AeroButton { Text = editorBlock.CtaText2, Url = editorBlock.CtaUrl2, Style = AeroButtonStyle.Secondary }
                ]
            },
            "aero_features" => new AeroFeaturesBlock
            {
                Title = editorBlock.MainText,
                SubTitle = editorBlock.SubText,
                Layout = Enum.TryParse<AeroFeaturesLayout>(editorBlock.AeroLayout, true, out var layout)
                    ? layout
                    : AeroFeaturesLayout.Simple,
                Items = editorBlock.FeatureItems.Select(f => new AeroFeatureItem
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
                Title = editorBlock.MainText,
                Description = string.IsNullOrWhiteSpace(editorBlock.SubText)
                    ? editorBlock.Description
                    : editorBlock.SubText,
                CtaText = editorBlock.CtaText,
                CtaUrl = editorBlock.CtaUrl,
                Layout = Enum.TryParse<AeroCtaLayout>(editorBlock.AeroLayout, true, out var layout)
                    ? layout
                    : AeroCtaLayout.Card
            },
            "aero_blog" => new AeroBlogBlock
            {
                Title = FirstNonEmpty(editorBlock.MainText, editorBlock.SectionTitle),
                Description = editorBlock.Description,
                Posts = editorBlock.BlogPosts.Select(p => new AeroBlogItem
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
                Title = FirstNonEmpty(editorBlock.MainText, editorBlock.PageTitle),
                Description = FirstNonEmpty(editorBlock.Description, editorBlock.PageDescription),
                Plans = editorBlock.PricingPlans.Select(p => new AeroPricingPlan
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
                Title = FirstNonEmpty(editorBlock.MainText, editorBlock.SectionTitle),
                Description = editorBlock.Description,
                Members = editorBlock.TeamMembers.Select(m => new AeroTeamMember
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
                Title = FirstNonEmpty(editorBlock.MainText, editorBlock.SectionTitle),
                Description = editorBlock.Description,
                Testimonials = editorBlock.Testimonials.Select(t => new AeroTestimonialItem
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
                Title = FirstNonEmpty(editorBlock.MainText, editorBlock.Title),
                Description = editorBlock.Description,
                Items = editorBlock.FaqItems.Select(f => new AeroFaqItem
                {
                    Question = f.Question,
                    Answer = f.Answer
                }).ToList()
            },
            "aero_portfolio" => new AeroPortfolioBlock
            {
                Title = FirstNonEmpty(editorBlock.MainText, editorBlock.Title),
                Description = editorBlock.Description,
                Items = editorBlock.PortfolioItems.Select(p => new AeroPortfolioItem
                {
                    ProjectTitle = p.ProjectTitle,
                    ProjectDescription = p.ProjectDescription,
                    ProjectImageUrl = p.ProjectImageUrl,
                    ProjectUrl = p.ProjectUrl,
                    ProjectCategory = p.ProjectCategory
                }).ToList()
            },
            "aero_contact" => new AeroContactBlock
            {
                Title = FirstNonEmpty(editorBlock.MainText, editorBlock.Title),
                Description = editorBlock.Description,
                Details = editorBlock.ContactDetails.Select(c => new AeroContactDetail
                {
                    Label = c.Label,
                    Value = c.Value,
                    Icon = c.Icon
                }).ToList()
            },
            "aero_table" => new AeroTableBlock
            {
                Title = FirstNonEmpty(editorBlock.MainText, editorBlock.Title),
                Description = editorBlock.Description,
                Headers = editorBlock.TableHeaders.Select(h => new AeroTableHeader { Label = h.Label }).ToList(),
                Rows = editorBlock.TableRows.Select(r => new AeroTableRow { Cells = r.Cells.ToList() }).ToList()
            },
            "aero_auth" => new AeroAuthBlock
            {
                Title = FirstNonEmpty(editorBlock.MainText, editorBlock.Title),
                Description = editorBlock.Description,
                SubmitButtonText = editorBlock.CtaText,
                AlternativeLinkText = editorBlock.AlternativeLinkText,
                AlternativeLinkUrl = editorBlock.AlternativeLinkUrl,
                BackgroundImageUrl = editorBlock.BackgroundImage
            },
            "boring_hero" => new BoringHeroBlock
            {
                FullWidth = true,
                Title = FirstNonEmpty(editorBlock.MainText, editorBlock.Title),
                Summary = FirstNonEmpty(editorBlock.SubText, editorBlock.Description),
                BackgroundImageUrl = editorBlock.BackgroundImage
            },
            "hero" => new HeroBlock
            {
                Title = editorBlock.MainText,
                SubTitle = editorBlock.SubText,
                CtaText = editorBlock.CtaText,
                CtaUrl = editorBlock.CtaUrl,
                BackgroundImageUrl = editorBlock.BackgroundImage,
                Height = editorBlock.Height,
                FullScreen = editorBlock.FullScreen
            },
            "raw_html" => new RawHtmlBlock
            {
                Content = editorBlock.Content
            },
            "markdown" => new MarkdownBlock
            {
                Content = editorBlock.Content
            },
            "dynamic_template" => new DynamicTemplateBlock
            {
                DefinitionVersion = 1,
                InlineTemplate = editorBlock.ScribanTemplate,
                Data = ParseJsonDocument(editorBlock.ScribanDataJson)
            },
            "rich_text" or "content" => new RichTextBlock
            {
                Content = editorBlock.Content
            },
            "text" or "heading" => new HeadingBlock
            {
                Text = FirstNonEmpty(editorBlock.Title, editorBlock.Content),
                Level = 2
            },
            "quote" => new QuoteBlock
            {
                Content = editorBlock.Content,
                Author = editorBlock.Author
            },
            "image" => new ImageBlock
            {
                Url = editorBlock.Src,
                AltText = editorBlock.Alt,
                Caption = editorBlock.Caption
            },
            "video" => new EmbedBlock
            {
                SourceUrl = FirstNonEmpty(editorBlock.Url, editorBlock.Src),
                EmbedType = "video",
                AutoPlay = editorBlock.AutoPlay
            },
            "gallery" or "carousel" => new CarouselBlock
            {
                Items = editorBlock.GalleryImages.Select(g => new CarouselItem
                {
                    AltText = g.Alt,
                    Caption = g.Src
                }).ToList(),
                AutoPlay = editorBlock.AutoPlay,
                ShowArrows = editorBlock.ShowArrows,
                ControlLocation = editorBlock.ControlLocation,
                Interval = editorBlock.CarouselInterval
            },
            "columns" => MapColumnsBlock(editorBlock),
            _ => null
        };
    }

    private static ColumnsBlock MapColumnsBlock(EditorBlock editorBlock)
    {
        var columnSpan = editorBlock.ColumnCount > 0
            ? Math.Max(1, 12 / editorBlock.ColumnCount)
            : 12;

        return new ColumnsBlock
        {
            Gap = editorBlock.Gap > 0 ? $"{editorBlock.Gap}px" : null,
            Columns = editorBlock.EditorColumns.Select(column => new ColumnItem
            {
                Span = columnSpan,
                Blocks = column.Blocks
                    .Select(MapNestedBlock)
                    .OfType<BlockBase>()
                    .ToList()
            }).ToList()
        };
    }

    private static BlockBase? MapNestedBlock(NestedBlock nestedBlock)
    {
        return nestedBlock.Type switch
        {
            "text" => new RichTextBlock { Content = nestedBlock.Content },
            "image" => new ImageBlock { Url = nestedBlock.Src, AltText = nestedBlock.Alt },
            "video" => new EmbedBlock
            {
                SourceUrl = FirstNonEmpty(nestedBlock.Url, nestedBlock.Src),
                EmbedType = "video",
                AutoPlay = false
            },
            "button" => new CtaBlock
            {
                Text = nestedBlock.Text,
                Url = nestedBlock.Url,
                Style = nestedBlock.Style
            },
            _ => null
        };
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }

    private static JsonDocument? ParseJsonDocument(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return JsonDocument.Parse("{}");
        }
    }
}
