using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Styles;

namespace Aero.Cms.Modules.Setup;

internal static class SeededPageCompositionFactory
{
    public static NeoPageNode CreateBidirectionalFeature()
    {
        var container = FeatureContainer();

        container.Children.Add(Text(
            "Build once. Adapt everywhere.",
            ContentDirection.LeftToRight));
        container.Children.Add(Text(
            "أنشئ مرة واحدة، وقدّم تجربة ممتازة في كل اتجاه.",
            ContentDirection.RightToLeft));
        container.Children.Add(Button("Explore Aero CMS", "/blog"));

        return container;
    }

    public static NeoPageNode CreateBidirectionalFeature(
        string ltrHeading,
        string ltrBody,
        string rtlHeading,
        string rtlBody,
        string buttonText,
        string buttonUrl)
    {
        var container = FeatureContainer();

        container.Children.Add(Text(ltrHeading, ContentDirection.LeftToRight));
        container.Children.Add(Text(ltrBody, ContentDirection.LeftToRight));
        container.Children.Add(Text(rtlHeading, ContentDirection.RightToLeft));
        container.Children.Add(Text(rtlBody, ContentDirection.RightToLeft));
        container.Children.Add(Button(buttonText, buttonUrl));

        return container;
    }

    public static NeoPageNode CreateFeatureSection(
        string heading,
        string body,
        string buttonText,
        string buttonUrl)
    {
        var container = SectionContainer();

        container.Children.Add(Text(heading, ContentDirection.LeftToRight));
        container.Children.Add(Text(body, ContentDirection.LeftToRight));
        container.Children.Add(Button(buttonText, buttonUrl));

        return container;
    }

    public static NeoPageNode CreateTextSection(string content)
    {
        var container = SectionContainer();
        container.Children.Add(Text(content, ContentDirection.LeftToRight));
        return container;
    }

    public static NeoPageNode CreateTwoColumnSection(
        string leftHeading,
        string leftBody,
        string rightHeading,
        string rightBody)
    {
        var outerContainer = new NeoPageNode
        {
            NodeId = Guid.NewGuid().ToString("N"),
            CatalogId = "primitive.container",
            Kind = NeoPageNodeKind.Container,
            Properties = new Dictionary<string, JsonElement>
            {
                ["layout"] = JsonSerializer.SerializeToElement("row"),
                ["gap"] = JsonSerializer.SerializeToElement(8)
            },
            Style = new ResponsiveNodeStyle
            {
                Base = new NodeStyle
                {
                    Direction = ContentDirection.LeftToRight,
                    MaximumWidth = new CssLength(72, CssLengthUnit.Rem),
                    Margin = new LogicalSpacing
                    {
                        BlockStart = new CssLength(3, CssLengthUnit.Rem),
                        BlockEnd = new CssLength(3, CssLengthUnit.Rem),
                        InlineStart = CssLength.Auto,
                        InlineEnd = CssLength.Auto
                    },
                    Padding = new LogicalSpacing
                    {
                        BlockStart = new CssLength(2, CssLengthUnit.Rem),
                        BlockEnd = new CssLength(2, CssLengthUnit.Rem),
                        InlineStart = new CssLength(2, CssLengthUnit.Rem),
                        InlineEnd = new CssLength(2, CssLengthUnit.Rem)
                    }
                }
            }
        };

        var leftColumn = ColumnContainer();
        leftColumn.Children.Add(Text(leftHeading, ContentDirection.LeftToRight));
        leftColumn.Children.Add(Text(leftBody, ContentDirection.LeftToRight));

        var rightColumn = ColumnContainer();
        rightColumn.Children.Add(Text(rightHeading, ContentDirection.LeftToRight));
        rightColumn.Children.Add(Text(rightBody, ContentDirection.LeftToRight));

        outerContainer.Children.Add(leftColumn);
        outerContainer.Children.Add(rightColumn);

        return outerContainer;
    }

    /// ========== Seed-data block factories (Phase 2b) ==========

    public static NeoPageNode CreateBoringHero(string title, string summary, string? backgroundImage = null, bool fullWidth = true)
    {
        var props = new Dictionary<string, JsonElement>
        {
            ["title"] = JsonSerializer.SerializeToElement(title),
            ["summary"] = JsonSerializer.SerializeToElement(summary),
            ["fullWidth"] = JsonSerializer.SerializeToElement(fullWidth)
        };
        if (backgroundImage is not null)
            props["backgroundImageUrl"] = JsonSerializer.SerializeToElement(backgroundImage);

        return new NeoPageNode
        {
            NodeId = Guid.NewGuid().ToString("N"),
            CatalogId = "boring_hero",
            Kind = NeoPageNodeKind.Primitive,
            Properties = props,
            Style = new ResponsiveNodeStyle
            {
                Base = new NodeStyle { Direction = ContentDirection.LeftToRight }
            },
            Children = []
        };
    }

    public static NeoPageNode CreateRichText(string content)
    {
        return new NeoPageNode
        {
            NodeId = Guid.NewGuid().ToString("N"),
            CatalogId = "content",
            Kind = NeoPageNodeKind.Primitive,
            Properties = new Dictionary<string, JsonElement>
            {
                ["content"] = JsonSerializer.SerializeToElement(content)
            },
            Style = new ResponsiveNodeStyle
            {
                Base = new NodeStyle { Direction = ContentDirection.LeftToRight }
            },
            Children = []
        };
    }

    public static NeoPageNode CreateHeadingBlock(string text)
    {
        return new NeoPageNode
        {
            NodeId = Guid.NewGuid().ToString("N"),
            CatalogId = "primitive.heading",
            Kind = NeoPageNodeKind.Primitive,
            Properties = new Dictionary<string, JsonElement>
            {
                ["text"] = JsonSerializer.SerializeToElement(text),
                ["level"] = JsonSerializer.SerializeToElement(2)
            },
            Style = new ResponsiveNodeStyle
            {
                Base = new NodeStyle { Direction = ContentDirection.LeftToRight }
            },
            Children = []
        };
    }

    public static NeoPageNode CreateCtaButton(string text, string url)
    {
        return new NeoPageNode
        {
            NodeId = Guid.NewGuid().ToString("N"),
            CatalogId = "primitive.button",
            Kind = NeoPageNodeKind.Primitive,
            Properties = new Dictionary<string, JsonElement>
            {
                ["text"] = JsonSerializer.SerializeToElement(text),
                ["url"] = JsonSerializer.SerializeToElement(url)
            },
            Style = new ResponsiveNodeStyle
            {
                Base = new NodeStyle { Direction = ContentDirection.LeftToRight }
            },
            Children = []
        };
    }

    /// <summary>Creates a root node for seeding a page's block tree.</summary>
    public static NeoPageNode CreatePageRoot(params NeoPageNode[] blocks)
    {
        return new NeoPageNode
        {
            NodeId = "page-root",
            CatalogId = "page.root",
            Kind = NeoPageNodeKind.Page,
            Children = new List<NeoPageNode>(blocks)
        };
    }

    /// ========== HTML-adjacent primitive factories ==========

    public static NeoPageNode CreateHeading(string text, int level = 2) =>
        Node(
            "primitive.heading",
            NeoPageNodeKind.Primitive,
            new Dictionary<string, JsonElement>
            {
                ["text"] = JsonSerializer.SerializeToElement(text),
                ["level"] = JsonSerializer.SerializeToElement(level)
            });

    public static NeoPageNode CreateBlockquote(string text, string? citation = null)
    {
        var props = new Dictionary<string, JsonElement>
        {
            ["text"] = JsonSerializer.SerializeToElement(text)
        };
        if (!string.IsNullOrWhiteSpace(citation))
            props["citation"] = JsonSerializer.SerializeToElement(citation);

        return Node("primitive.blockquote", NeoPageNodeKind.Primitive, props);
    }

    public static NeoPageNode CreateSemanticSection(params NeoPageNode[] children)
    {
        var section = Node("primitive.section", NeoPageNodeKind.Container);
        foreach (var child in children)
            section.Children.Add(child);
        return section;
    }

    public static NeoPageNode CreateGridTwoColumnSection(
        string leftHeading,
        string leftBody,
        string rightHeading,
        string rightBody)
    {
        var grid = Node(
            "primitive.grid",
            NeoPageNodeKind.Container,
            new Dictionary<string, JsonElement>
            {
                ["columns"] = JsonSerializer.SerializeToElement(12),
                ["gap"] = JsonSerializer.SerializeToElement(8)
            },
            new ResponsiveNodeStyle
            {
                Base = new NodeStyle
                {
                    Direction = ContentDirection.LeftToRight,
                    MaximumWidth = new CssLength(72, CssLengthUnit.Rem),
                    Margin = new LogicalSpacing
                    {
                        BlockStart = new CssLength(3, CssLengthUnit.Rem),
                        BlockEnd = new CssLength(3, CssLengthUnit.Rem),
                        InlineStart = CssLength.Auto,
                        InlineEnd = CssLength.Auto
                    }
                }
            });

        var row = Node("primitive.grid-row", NeoPageNodeKind.Container);

        var leftCell = Node(
            "primitive.grid-cell",
            NeoPageNodeKind.Container,
            new Dictionary<string, JsonElement> { ["span"] = JsonSerializer.SerializeToElement(6) });
        leftCell.Children.Add(CreateHeading(leftHeading, 3));
        leftCell.Children.Add(Text(leftBody, ContentDirection.LeftToRight));

        var rightCell = Node(
            "primitive.grid-cell",
            NeoPageNodeKind.Container,
            new Dictionary<string, JsonElement> { ["span"] = JsonSerializer.SerializeToElement(6) });
        rightCell.Children.Add(CreateHeading(rightHeading, 3));
        rightCell.Children.Add(Text(rightBody, ContentDirection.LeftToRight));

        row.Children.Add(leftCell);
        row.Children.Add(rightCell);
        grid.Children.Add(row);

        return grid;
    }

    public static NeoPageNode CreateSemanticPageLayout(
        string headerTitle,
        string navLink1, string navLink2, string navLink3,
        string mainHeading, string mainBody)
    {
        var page = Node("primitive.section", NeoPageNodeKind.Container);

        // Header
        var header = Node("primitive.header", NeoPageNodeKind.Container);
        header.Children.Add(CreateHeading(headerTitle, 1));
        page.Children.Add(header);

        // Nav
        var nav = Node("primitive.nav", NeoPageNodeKind.Container);
        nav.Children.Add(Button(navLink1, "/"));
        nav.Children.Add(Button(navLink2, "/about"));
        nav.Children.Add(Button(navLink3, "/contact"));
        page.Children.Add(nav);

        // Main section
        var main = Node("primitive.section", NeoPageNodeKind.Container);
        main.Children.Add(CreateHeading(mainHeading, 2));
        main.Children.Add(Text(mainBody, ContentDirection.LeftToRight));
        page.Children.Add(main);

        // Footer
        var footer = Node("primitive.footer", NeoPageNodeKind.Container);
        footer.Children.Add(Text($"© {DateTime.UtcNow.Year} — Built with AeroCMS", ContentDirection.LeftToRight));
        page.Children.Add(footer);

        return page;
    }

    private static NeoPageNode FeatureContainer() =>
        Node(
            "primitive.container",
            NeoPageNodeKind.Container,
            new Dictionary<string, JsonElement>
            {
                ["layout"] = JsonSerializer.SerializeToElement("stack"),
                ["gap"] = JsonSerializer.SerializeToElement(4)
            },
            new ResponsiveNodeStyle
            {
                Base = new NodeStyle
                {
                    Direction = ContentDirection.LeftToRight,
                    MaximumWidth = new CssLength(72, CssLengthUnit.Rem),
                    Margin = new LogicalSpacing
                    {
                        BlockStart = new CssLength(3, CssLengthUnit.Rem),
                        BlockEnd = new CssLength(3, CssLengthUnit.Rem),
                        InlineStart = CssLength.Auto,
                        InlineEnd = CssLength.Auto
                    },
                    Padding = new LogicalSpacing
                    {
                        BlockStart = new CssLength(2, CssLengthUnit.Rem),
                        BlockEnd = new CssLength(2, CssLengthUnit.Rem),
                        InlineStart = new CssLength(2, CssLengthUnit.Rem),
                        InlineEnd = new CssLength(2, CssLengthUnit.Rem)
                    }
                },
                Mobile = new NodeStyleOverride
                {
                    Padding = new LogicalSpacingOverride
                    {
                        InlineStart = new CssLength(1, CssLengthUnit.Rem),
                        InlineEnd = new CssLength(1, CssLengthUnit.Rem)
                    }
                }
            });

    private static NeoPageNode SectionContainer() =>
        Node(
            "primitive.container",
            NeoPageNodeKind.Container,
            new Dictionary<string, JsonElement>
            {
                ["layout"] = JsonSerializer.SerializeToElement("stack"),
                ["gap"] = JsonSerializer.SerializeToElement(4)
            },
            new ResponsiveNodeStyle
            {
                Base = new NodeStyle
                {
                    Direction = ContentDirection.LeftToRight,
                    MaximumWidth = new CssLength(72, CssLengthUnit.Rem),
                    Margin = new LogicalSpacing
                    {
                        BlockStart = new CssLength(3, CssLengthUnit.Rem),
                        BlockEnd = new CssLength(3, CssLengthUnit.Rem),
                        InlineStart = CssLength.Auto,
                        InlineEnd = CssLength.Auto
                    },
                    Padding = new LogicalSpacing
                    {
                        BlockStart = new CssLength(2, CssLengthUnit.Rem),
                        BlockEnd = new CssLength(2, CssLengthUnit.Rem),
                        InlineStart = new CssLength(2, CssLengthUnit.Rem),
                        InlineEnd = new CssLength(2, CssLengthUnit.Rem)
                    }
                }
            });

    private static NeoPageNode ColumnContainer() =>
        Node(
            "primitive.container",
            NeoPageNodeKind.Container,
            new Dictionary<string, JsonElement>
            {
                ["layout"] = JsonSerializer.SerializeToElement("stack"),
                ["gap"] = JsonSerializer.SerializeToElement(4)
            },
            new ResponsiveNodeStyle
            {
                Base = new NodeStyle
                {
                    Direction = ContentDirection.LeftToRight
                }
            });

    private static NeoPageNode Button(string text, string url) =>
        Node(
            "primitive.button",
            NeoPageNodeKind.Primitive,
            new Dictionary<string, JsonElement>
            {
                ["text"] = JsonSerializer.SerializeToElement(text),
                ["url"] = JsonSerializer.SerializeToElement(url)
            });

    private static NeoPageNode Text(string text, ContentDirection direction) =>
        Node(
            "primitive.text",
            NeoPageNodeKind.Primitive,
            new Dictionary<string, JsonElement>
            {
                ["text"] = JsonSerializer.SerializeToElement(text)
            },
            new ResponsiveNodeStyle
            {
                Base = new NodeStyle { Direction = direction }
            });

    private static NeoPageNode Node(
        string catalogId,
        NeoPageNodeKind kind,
        Dictionary<string, JsonElement>? properties = null,
        ResponsiveNodeStyle? style = null) =>
        new()
        {
            NodeId = Guid.NewGuid().ToString("N"),
            CatalogId = catalogId,
            Kind = kind,
            Properties = properties ?? [],
            Style = style ?? new ResponsiveNodeStyle()
        };
}
