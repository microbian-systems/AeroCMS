using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Html;

/// <summary>
/// Creates first-release layout starters from catalog-supported ordinary HTML nodes.
/// </summary>
/// <param name="catalog">The authoritative catalog used to create every emitted element.</param>
public sealed class HtmlLayoutStarterFactory(HtmlElementCatalog catalog) : IHtmlLayoutStarterFactory
{
    /// <inheritdoc />
    public Result<HtmlNode> Create(HtmlLayoutStarterKind kind)
    {
        HtmlNode? layout = kind switch
        {
            HtmlLayoutStarterKind.OneColumn => CreateGrid(columns: 1, childCount: 1, childTag: "div"),
            HtmlLayoutStarterKind.TwoColumns => CreateGrid(columns: 2, childCount: 2, childTag: "div"),
            HtmlLayoutStarterKind.ThreeColumns => CreateGrid(columns: 3, childCount: 3, childTag: "div"),
            HtmlLayoutStarterKind.FourColumns => CreateGrid(columns: 4, childCount: 4, childTag: "div"),
            HtmlLayoutStarterKind.Split => CreateSplit(),
            HtmlLayoutStarterKind.HeadingTwoColumns => CreateHeadingTwoColumns(),
            HtmlLayoutStarterKind.CardGrid => CreateGrid(columns: 3, childCount: 3, childTag: "article"),
            _ => null
        };

        return layout is null
            ? new Result<HtmlNode>.Failure(AeroError.ValidationError(["The requested layout starter is not supported."]))
            : new Result<HtmlNode>.Ok(layout);
    }

    /// <summary>Builds a responsive grid with the requested number of empty catalog-backed child containers.</summary>
    private HtmlNode CreateGrid(int columns, int childCount, string childTag)
    {
        var section = catalog.CreateElement("section");
        var container = catalog.CreateElement("div");
        container.Style = new HtmlStyle
        {
            Display = CssDisplay.Grid,
            GridColumns = columns,
            StackOnSmallScreens = columns > 1,
            Gap = CssLength.Rem(1.5m)
        };

        for (var index = 0; index < childCount; index++)
        {
            container.Children.Add(catalog.CreateElement(childTag));
        }

        section.Children.Add(container);
        return section;
    }

    /// <summary>Builds an asymmetric two-region starter intended for copy and media.</summary>
    private HtmlNode CreateSplit()
    {
        var section = catalog.CreateElement("section");
        var container = catalog.CreateElement("div");
        container.Style = new HtmlStyle
        {
            Display = CssDisplay.Flex,
            FlexDirection = CssFlexDirection.Row,
            StackOnSmallScreens = true,
            Gap = CssLength.Rem(2)
        };
        container.Children.Add(catalog.CreateElement("div"));
        container.Children.Add(catalog.CreateElement("div"));
        section.Children.Add(container);
        return section;
    }

    /// <summary>Builds a heading followed by a responsive two-column content region.</summary>
    private HtmlNode CreateHeadingTwoColumns()
    {
        var section = catalog.CreateElement("section");
        var heading = catalog.CreateElement("div");
        heading.Children.Add(catalog.CreateElement("h2"));
        heading.Children.Add(catalog.CreateElement("p"));

        var columns = catalog.CreateElement("div");
        columns.Style = new HtmlStyle
        {
            Display = CssDisplay.Grid,
            GridColumns = 2,
            StackOnSmallScreens = true,
            Gap = CssLength.Rem(1.5m),
            Margin = new CssLogicalSpacing { BlockStart = CssLength.Rem(1.5m) }
        };
        columns.Children.Add(catalog.CreateElement("div"));
        columns.Children.Add(catalog.CreateElement("div"));

        section.Children.Add(heading);
        section.Children.Add(columns);
        return section;
    }
}
