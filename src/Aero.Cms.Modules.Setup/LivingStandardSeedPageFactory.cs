using Aero.Cms.Html;

namespace Aero.Cms.Modules.Setup;

/// <summary>
/// Creates framework-neutral starter pages using the same HTML tree edited by the page builder.
/// </summary>
internal static class LivingStandardSeedPageFactory
{
    public static HtmlPageContent Create(
        string title,
        string summary,
        IReadOnlyList<(string Heading, string Body)> sections,
        string? backgroundImageUrl = null,
        (string Label, string Href)? callToAction = null)
    {
        var content = new HtmlPageContent();
        var main = HtmlNode.CreateElement("main");
        main.Children.Add(CreateHero(title, summary, backgroundImageUrl));

        if (sections.Count > 0)
        {
            var sectionGrid = HtmlNode.CreateElement("section");
            sectionGrid.Style = new HtmlStyle
            {
                Display = CssDisplay.Grid,
                GridColumns = sections.Count > 1 ? 2 : 1,
                StackOnSmallScreens = true,
                Gap = CssLength.Rem(2),
                Padding = AllSides(2)
            };

            foreach (var (heading, body) in sections)
            {
                var article = HtmlNode.CreateElement("article");
                article.Style = new HtmlStyle
                {
                    Padding = AllSides(1.5m),
                    Surface = new CssSurfaceStyle
                    {
                        BackgroundColor = CssColor.Hex("#ffffff"),
                        BorderRadius = CssLength.Rem(0.75m)
                    }
                };
                article.Children.Add(TextElement("h2", heading, fontSizeRem: 1.75m, fontWeight: 700));
                article.Children.Add(TextElement("p", body, fontSizeRem: 1.05m, lineHeight: 1.7m));
                sectionGrid.Children.Add(article);
            }

            main.Children.Add(sectionGrid);
        }

        if (callToAction is { } cta)
        {
            var ctaSection = HtmlNode.CreateElement("section");
            ctaSection.Style = new HtmlStyle
            {
                Display = CssDisplay.Flex,
                JustifyContent = CssJustification.Center,
                Padding = AllSides(2)
            };
            var link = TextElement("a", cta.Label, fontSizeRem: 1.1m, fontWeight: 700);
            link.Attributes["href"] = cta.Href;
            link.Style!.Padding = AllSides(0.75m);
            ctaSection.Children.Add(link);
            main.Children.Add(ctaSection);
        }

        content.Root.Children.Add(main);
        return content;
    }

    private static HtmlNode CreateHero(string title, string summary, string? backgroundImageUrl)
    {
        var hero = HtmlNode.CreateElement("section");
        hero.Style = new HtmlStyle
        {
            Display = CssDisplay.Flex,
            FlexDirection = CssFlexDirection.Column,
            AlignItems = CssAlignment.Center,
            JustifyContent = CssJustification.Center,
            Gap = CssLength.Rem(1),
            MinimumHeight = CssLength.Rem(22),
            Padding = AllSides(2),
            Surface = new CssSurfaceStyle
            {
                BackgroundColor = CssColor.Hex("#eef2ff"),
                BackgroundImageUrl = backgroundImageUrl,
                BackgroundFit = backgroundImageUrl is null ? null : CssBackgroundFit.Cover,
                BackgroundPosition = backgroundImageUrl is null ? null : CssBackgroundPosition.Center,
                BackgroundRepeat = backgroundImageUrl is null ? null : CssBackgroundRepeat.NoRepeat,
                OverlayColor = backgroundImageUrl is null ? null : CssColor.Hex("#111827"),
                OverlayOpacity = backgroundImageUrl is null ? null : 0.62m
            }
        };

        hero.Children.Add(TextElement("h1", title, fontSizeRem: 3m, fontWeight: 800, centered: true));
        hero.Children.Add(TextElement("p", summary, fontSizeRem: 1.25m, lineHeight: 1.6m, centered: true));
        return hero;
    }

    private static HtmlNode TextElement(
        string tag,
        string text,
        decimal? fontSizeRem = null,
        int? fontWeight = null,
        decimal? lineHeight = null,
        bool centered = false)
    {
        var element = HtmlNode.CreateElement(tag);
        element.Style = new HtmlStyle
        {
            Typography = new CssTypographyStyle
            {
                FontSize = fontSizeRem is null ? null : CssLength.Rem(fontSizeRem.Value),
                FontWeight = fontWeight,
                LineHeight = lineHeight,
                Alignment = centered ? CssTextAlignment.Center : null
            }
        };
        element.Children.Add(HtmlNode.CreateText(text));
        return element;
    }

    private static CssLogicalSpacing AllSides(decimal rem) => new()
    {
        BlockStart = CssLength.Rem(rem),
        InlineEnd = CssLength.Rem(rem),
        BlockEnd = CssLength.Rem(rem),
        InlineStart = CssLength.Rem(rem)
    };
}
