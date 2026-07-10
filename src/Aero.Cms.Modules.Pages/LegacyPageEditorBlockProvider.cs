using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using System.Text.Json;

namespace Aero.Cms.Modules.Pages;

/// <summary>
/// Transitional catalog provider for legacy page-editor block ids that used to
/// exist only in switch statements. This keeps old canned blocks discoverable
/// through the same registry contract that package-provided blocks use.
/// </summary>
public sealed class LegacyPageEditorBlockProvider : IPageEditorBlockProvider
{
    private static readonly IReadOnlyCollection<IPageEditorBlockDefinition> Definitions =
    [
        Define("aero_features", "Aero Features", "Aero", "layers", 110, editorBlock => new AeroFeaturesBlock
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
        }),
        Define("aero_cta", "Aero CTA", "Aero", "megaphone", 120, editorBlock => new AeroCtaBlock
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
        }),
        Define("aero_blog", "Aero Blog", "Aero", "article", 130, editorBlock => new AeroBlogBlock
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
        }),
        Define("aero_pricing", "Aero Pricing", "Aero", "currency-dollar", 140, editorBlock => new AeroPricingBlock
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
        }),
        Define("aero_teams", "Aero Teams", "Aero", "users", 150, editorBlock => new AeroTeamsBlock
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
        }),
        Define("aero_testimonials", "Aero Testimonials", "Aero", "chat-bubble-left-right", 160, editorBlock => new AeroTestimonialsBlock
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
        }),
        Define("aero_faq", "Aero FAQ", "Aero", "question-mark-circle", 170, editorBlock => new AeroFaqBlock
        {
            Title = FirstNonEmpty(editorBlock.MainText, editorBlock.Title),
            Description = editorBlock.Description,
            Items = editorBlock.FaqItems.Select(f => new AeroFaqItem
            {
                Question = f.Question,
                Answer = f.Answer
            }).ToList()
        }),
        Define("aero_portfolio", "Aero Portfolio", "Aero", "briefcase", 180, editorBlock => new AeroPortfolioBlock
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
        }),
        Define("aero_contact", "Aero Contact", "Aero", "envelope", 190, editorBlock => new AeroContactBlock
        {
            Title = FirstNonEmpty(editorBlock.MainText, editorBlock.Title),
            Description = editorBlock.Description,
            Details = editorBlock.ContactDetails.Select(c => new AeroContactDetail
            {
                Label = c.Label,
                Value = c.Value,
                Icon = c.Icon
            }).ToList()
        }),
        Define("aero_table", "Aero Table", "Aero", "table-cells", 200, editorBlock => new AeroTableBlock
        {
            Title = FirstNonEmpty(editorBlock.MainText, editorBlock.Title),
            Description = editorBlock.Description,
            Headers = editorBlock.TableHeaders.Select(h => new AeroTableHeader { Label = h.Label }).ToList(),
            Rows = editorBlock.TableRows.Select(r => new AeroTableRow { Cells = r.Cells.ToList() }).ToList()
        }),
        Define("aero_auth", "Aero Auth", "Aero", "shield-check", 210, editorBlock => new AeroAuthBlock
        {
            Title = FirstNonEmpty(editorBlock.MainText, editorBlock.Title),
            Description = editorBlock.Description,
            SubmitButtonText = editorBlock.CtaText,
            AlternativeLinkText = editorBlock.AlternativeLinkText,
            AlternativeLinkUrl = editorBlock.AlternativeLinkUrl,
            BackgroundImageUrl = editorBlock.BackgroundImage
        }),
        Define("boring_hero", "Boring Hero", "UI", "image", 300, editorBlock => new BoringHeroBlock
        {
            FullWidth = true,
            Title = FirstNonEmpty(editorBlock.MainText, editorBlock.Title),
            Summary = FirstNonEmpty(editorBlock.SubText, editorBlock.Description),
            BackgroundImageUrl = editorBlock.BackgroundImage
        }),
        Define("hero", "Hero", "UI", "image", 310, editorBlock => new HeroBlock
        {
            Title = editorBlock.MainText,
            SubTitle = editorBlock.SubText,
            CtaText = editorBlock.CtaText,
            CtaUrl = editorBlock.CtaUrl,
            BackgroundImageUrl = editorBlock.BackgroundImage,
            Height = editorBlock.Height,
            FullScreen = editorBlock.FullScreen
        }),
        Define("raw_html", "Raw HTML", "Advanced", "code-bracket", 400, editorBlock => new RawHtmlBlock
        {
            Content = editorBlock.Content
        }),
        Define("markdown", "Markdown", "Text", "document-text", 410, editorBlock => new MarkdownBlock
        {
            Content = editorBlock.Content
        }),
        Define("dynamic_template", "Dynamic Template", "Dynamic", "code-bracket-square", 420, editorBlock => new DynamicTemplateBlock
        {
            DefinitionVersion = 1,
            InlineTemplate = editorBlock.ScribanTemplate,
            Data = ParseJsonDocument(editorBlock.ScribanDataJson)
        }),
        Define("rich_text", "Rich Text", "Text", "document-text", 430, editorBlock => new RichTextBlock
        {
            Content = editorBlock.Content
        }),
        Define("content", "Content", "Text", "document-text", 431, editorBlock => new RichTextBlock
        {
            Content = editorBlock.Content
        }),
        Define("text", "Text", "Text", "bars-3-bottom-left", 440, editorBlock => new HeadingBlock
        {
            Text = FirstNonEmpty(editorBlock.Title, editorBlock.Content),
            Level = 2
        }),
        Define("heading", "Heading", "Text", "h1", 441, editorBlock => new HeadingBlock
        {
            Text = FirstNonEmpty(editorBlock.Title, editorBlock.Content),
            Level = 2
        }),
        Define("quote", "Quote", "Text", "chat-bubble-left-ellipsis", 450, editorBlock => new QuoteBlock
        {
            Content = editorBlock.Content,
            Author = editorBlock.Author
        }),
        Define("video", "Video", "Media", "video-camera", 510, editorBlock => new Aero.Cms.Abstractions.Blocks.Neo.VideoBlock
        {
            Src = FirstNonEmpty(editorBlock.Url, editorBlock.Src),
            Autoplay = editorBlock.AutoPlay,
            Controls = true
        }),
        Define("audio", "Audio", "Media", "speaker-wave", 520, editorBlock => new Aero.Cms.Abstractions.Blocks.Neo.AudioBlock
        {
            Src = editorBlock.Src,
            Controls = true
        }),
        Define("gallery", "Gallery", "Media", "squares-2x2", 530, editorBlock => new Aero.Cms.Abstractions.Blocks.Neo.GalleryBlock
        {
            Images = editorBlock.GalleryImages.Select(g => g.Src).Where(s => !string.IsNullOrWhiteSpace(s)).ToList(),
            Columns = 3
        }),
        Define("carousel", "Carousel", "Media", "rectangle-stack", 540, editorBlock => new CarouselBlock
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
        }),
        Define("columns", "Columns", "Layout", "columns-3", 600, MapColumnsBlock)
    ];

        /// <summary>
    /// GetDefinitions method.
    /// </summary>
public IReadOnlyCollection<IPageEditorBlockDefinition> GetDefinitions() => Definitions;

    private static IPageEditorBlockDefinition Define(
        string catalogId,
        string displayName,
        string category,
        string iconName,
        int sortOrder,
        Func<EditorBlock, BlockBase?> toBlockBase) =>
        new LegacyAliasEditorBlockDefinition(catalogId, displayName, category, iconName, sortOrder, toBlockBase);

    private static ColumnsBlock MapColumnsBlock(EditorBlock editorBlock)
    {
        var columnSpan = editorBlock.ColumnCount > 0
            ? Math.Max(1, 12 / editorBlock.ColumnCount)
            : 12;

        return new ColumnsBlock
        {
            Gap = editorBlock.Gap > 0 ? $"{editorBlock.Gap}px" : null,
            ColumnCount = Math.Max(1, editorBlock.ColumnCount),
            RowCount = Math.Max(1, editorBlock.RowCount),
            Columns = editorBlock.EditorColumns.Select(column => new Aero.Cms.Abstractions.Blocks.Common.ColumnItem
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
            "image" => new Aero.Cms.Abstractions.Blocks.Neo.ImageBlock { Src = nestedBlock.Src, Alt = nestedBlock.Alt },
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

    /// <summary>
    /// Concrete legacy definition used by <see cref="LegacyPageEditorBlockProvider"/>.
    /// It supplies catalog metadata, a default editor DTO, a lightweight Neo node
    /// projection, and the legacy BlockBase mapping delegate.
    /// </summary>
    private sealed class LegacyAliasEditorBlockDefinition(
        string catalogId,
        string displayName,
        string category,
        string iconName,
        int sortOrder,
        Func<EditorBlock, BlockBase?> toBlockBase) : IPageEditorBlockDefinition
    {
                /// <summary>
        /// Gets or sets the Catalog Id.
        /// </summary>
public string CatalogId { get; } = catalogId;
                /// <summary>
        /// Gets or sets the Display Name.
        /// </summary>
public string DisplayName { get; } = displayName;
                /// <summary>
        /// Gets or sets the Description.
        /// </summary>
public string? Description => "Legacy canned block registered through the unified page-editor catalog.";
                /// <summary>
        /// Gets or sets the Category.
        /// </summary>
public string Category { get; } = category;
                /// <summary>
        /// Gets or sets the Kind.
        /// </summary>
public string Kind => "Block";
                /// <summary>
        /// Gets or sets the Icon Name.
        /// </summary>
public string IconName { get; } = iconName;
                /// <summary>
        /// Gets or sets the Sort Order.
        /// </summary>
public int SortOrder { get; } = sortOrder;
                /// <summary>
        /// Gets or sets the Public Static Ssr Safe.
        /// </summary>
public bool PublicStaticSsrSafe => true;
                /// <summary>
        /// Gets or sets the Preview Component Type.
        /// </summary>
public Type? PreviewComponentType => null;
                /// <summary>
        /// Gets or sets the Property Editor Component Type.
        /// </summary>
public Type? PropertyEditorComponentType => null;

                /// <summary>
        /// CreateDefaultEditorBlock method.
        /// </summary>
public EditorBlock CreateDefaultEditorBlock() => new()
        {
            Type = CatalogId,
            Title = DisplayName,
            MainText = DisplayName,
            Content = string.Empty,
            ColumnCount = CatalogId == "columns" ? 2 : 1,
            RowCount = 1
        };

                /// <summary>
        /// ToNeoPageNode method.
        /// </summary>
public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
        {
            ArgumentNullException.ThrowIfNull(editorBlock);

            return new NeoPageNode
            {
                NodeId = Guid.NewGuid().ToString("N"),
                CatalogId = CatalogId,
                Kind = NeoPageNodeKind.Section,
                Style = editorBlock.Style.DeepClone(),
                Properties = new Dictionary<string, JsonElement>
                {
                    ["title"] = JsonSerializer.SerializeToElement(FirstNonEmpty(editorBlock.MainText, editorBlock.Title)),
                    ["description"] = JsonSerializer.SerializeToElement(FirstNonEmpty(editorBlock.SubText, editorBlock.Description)),
                    ["content"] = JsonSerializer.SerializeToElement(editorBlock.Content ?? string.Empty),
                    ["src"] = JsonSerializer.SerializeToElement(FirstNonEmpty(editorBlock.Src, editorBlock.Url, editorBlock.BackgroundImage))
                }
            };
        }

                /// <summary>
        /// ToBlockBase method.
        /// </summary>
public BlockBase? ToBlockBase(EditorBlock editorBlock)
        {
            ArgumentNullException.ThrowIfNull(editorBlock);
            return toBlockBase(editorBlock);
        }
    }
}
