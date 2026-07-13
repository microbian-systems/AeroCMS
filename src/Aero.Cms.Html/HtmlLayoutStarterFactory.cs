using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Html;

/// <summary>
/// Creates first-release layout starters from catalog-supported ordinary HTML nodes.
/// </summary>
public sealed class HtmlLayoutStarterFactory(HtmlElementCatalog catalog) : IHtmlLayoutStarterFactory
{
    public Result<HtmlNode> Create(HtmlLayoutStarterKind kind)
    {
        HtmlNode? layout = kind switch
        {
            HtmlLayoutStarterKind.OneColumn => CreateGrid(columns: 1, childCount: 1, childTag: "div"),
            HtmlLayoutStarterKind.TwoColumns => CreateGrid(columns: 2, childCount: 2, childTag: "div"),
            HtmlLayoutStarterKind.ThreeColumns => CreateGrid(columns: 3, childCount: 3, childTag: "div"),
            HtmlLayoutStarterKind.Split => CreateSplit(),
            HtmlLayoutStarterKind.CardGrid => CreateGrid(columns: 3, childCount: 3, childTag: "article"),
            _ => null
        };

        return layout is null
            ? new Result<HtmlNode>.Failure(AeroError.ValidationError(["The requested layout starter is not supported."]))
            : new Result<HtmlNode>.Ok(layout);
    }

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
}
