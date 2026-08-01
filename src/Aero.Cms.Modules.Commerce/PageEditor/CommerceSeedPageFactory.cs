using System.Text.Json;
using Aero.Cms.Abstractions.Pages.Composition;
using Aero.Cms.Html;
using Aero.Cms.Modules.Commerce.Catalog.Models;
using Aero.Cms.Shared.Localization;

namespace Aero.Cms.Modules.Commerce.PageEditor;

/// <summary>Builds semantic, editable Commerce page shells and their typed application fragments.</summary>
internal static class CommerceSeedPageFactory
{
    public static (HtmlPageContent Content, PageCompositionDocument Composition) CreateCatalog(
        string title,
        string summary,
        bool featuredOnly = false,
        string? culture = null,
        string? defaultCulture = null)
    {
        var (content, main) = CreateShell(title, summary, culture, defaultCulture);
        var host = Element("section", new HtmlStyle
        {
            Padding = AllSides(2),
            Surface = new CssSurfaceStyle
            {
                BackgroundColor = CssColor.Hex("#ffffff"),
                BorderRadius = CssLength.Rem(0.75m)
            }
        });
        main.Children.Add(host);
        return (content, new PageCompositionDocument
        {
            RegisteredFragments =
            [
                new PageRegisteredFragment
                {
                    NodeId = host.NodeId,
                    Key = "commerce.catalog",
                    Parameters = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                    {
                        ["take"] = JsonSerializer.SerializeToElement(12),
                        ["featuredOnly"] = JsonSerializer.SerializeToElement(featuredOnly)
                    }
                }
            ]
        });
    }

    public static (HtmlPageContent Content, PageCompositionDocument Composition) CreateSearch(
        string? culture = null,
        string? defaultCulture = null)
    {
        var (content, main) = CreateShell("Search the shop", "Find published products available for this storefront.", culture, defaultCulture);
        var host = Element("section", new HtmlStyle
        {
            Padding = AllSides(2),
            Surface = new CssSurfaceStyle
            {
                BackgroundColor = CssColor.Hex("#ffffff"),
                BorderRadius = CssLength.Rem(0.75m)
            }
        });
        main.Children.Add(host);
        return (content, new PageCompositionDocument
        {
            RegisteredFragments =
            [
                new PageRegisteredFragment
                {
                    NodeId = host.NodeId,
                    Key = "commerce.search",
                    Parameters = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                    {
                        ["take"] = JsonSerializer.SerializeToElement(12)
                    }
                }
            ]
        });
    }

    public static (HtmlPageContent Content, PageCompositionDocument Composition) CreateProduct(
        string slug,
        string? culture = null,
        string? defaultCulture = null)
    {
        // The shell is a durable Page document, while listing visibility can change at any
        // time. Keep all product data in the visibility-scoped fragment below so unpublishing
        // or deactivating a listing cannot leave stale public text or SEO metadata behind.
        var (content, main) = CreateShell(
            "Storefront product",
            "Product availability and details are shown from the current storefront catalog.",
            culture,
            defaultCulture);
        var host = Element("section", new HtmlStyle
        {
            Padding = AllSides(2),
            Surface = new CssSurfaceStyle
            {
                BackgroundColor = CssColor.Hex("#ffffff"),
                BorderRadius = CssLength.Rem(0.75m)
            }
        });
        main.Children.Add(host);
        return (content, new PageCompositionDocument
        {
            RegisteredFragments =
            [
                new PageRegisteredFragment
                {
                    NodeId = host.NodeId,
                    Key = "commerce.product",
                    Parameters = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                    {
                        ["slug"] = JsonSerializer.SerializeToElement(slug)
                    }
                }
            ]
        });
    }

    private static (HtmlPageContent Content, HtmlNode Main) CreateShell(
        string title,
        string summary,
        string? culture,
        string? defaultCulture)
    {
        var content = new HtmlPageContent();
        var main = Element("main", new HtmlStyle
        {
            Padding = AllSides(2),
            Gap = CssLength.Rem(1.5m),
            Display = CssDisplay.Grid
        });
        var header = Element("header", new HtmlStyle
        {
            Padding = AllSides(2),
            Surface = new CssSurfaceStyle
            {
                BackgroundColor = CssColor.Hex("#f4f4f5"),
                BorderRadius = CssLength.Rem(0.75m)
            }
        });
        header.Children.Add(Text("h1", title, 2.4m, 800));
        header.Children.Add(Text("p", summary, 1.1m, null));
        var navigation = Element("nav", new HtmlStyle
        {
            Display = CssDisplay.Flex,
            Gap = CssLength.Rem(1),
            Margin = new CssLogicalSpacing { BlockStart = CssLength.Rem(1) }
        });
        navigation.Attributes["aria-label"] = "Shop navigation";
        var routeCulture = AeroCultureRoute.NormalizeCultureOrDefault(culture, defaultCulture ?? SitesModel.DefaultCultureName);
        navigation.Children.Add(Link("Shop home", AeroCultureRoute.BuildCulturePath(routeCulture, "shop")));
        navigation.Children.Add(Link("Browse products", AeroCultureRoute.BuildCulturePath(routeCulture, "shop/products")));
        navigation.Children.Add(Link("Search", AeroCultureRoute.BuildCulturePath(routeCulture, "shop/search")));
        header.Children.Add(navigation);
        main.Children.Add(header);
        content.Root.Children.Add(main);
        return (content, main);
    }

    private static HtmlNode Link(string label, string href)
    {
        var link = Element("a", new HtmlStyle
        {
            Typography = new CssTypographyStyle { FontWeight = 700 }
        });
        link.Attributes["href"] = href;
        link.Children.Add(HtmlNode.CreateText(label));
        return link;
    }

    private static HtmlNode Text(string tag, string value, decimal fontSize, int? fontWeight)
    {
        var element = Element(tag, new HtmlStyle
        {
            Typography = new CssTypographyStyle
            {
                FontSize = CssLength.Rem(fontSize),
                FontWeight = fontWeight,
                LineHeight = 1.5m
            }
        });
        element.Children.Add(HtmlNode.CreateText(value));
        return element;
    }

    private static HtmlNode Element(string tag, HtmlStyle? style = null)
    {
        var node = HtmlNode.CreateElement(tag);
        node.Style = style;
        return node;
    }

    private static CssLogicalSpacing AllSides(decimal rem) => new()
    {
        BlockStart = CssLength.Rem(rem),
        InlineEnd = CssLength.Rem(rem),
        BlockEnd = CssLength.Rem(rem),
        InlineStart = CssLength.Rem(rem)
    };
}
