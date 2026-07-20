using Aero.Cms.Html;

namespace Aero.Cms.Modules.Setup;

/// <summary>
/// Creates framework-neutral starter pages using the same HTML tree edited by the page builder.
/// </summary>
internal static class LivingStandardSeedPageFactory
{
    /// <summary>
    /// Builds a page composition containing a hero, an optional grid of content sections,
    /// and an optional call-to-action link.
    /// </summary>
    /// <param name="title">The hero heading text.</param>
    /// <param name="summary">The hero summary text.</param>
    /// <param name="sections">Ordered heading and body pairs rendered as articles.</param>
    /// <param name="backgroundImageUrl">An optional hero background image URL.</param>
    /// <param name="callToAction">An optional link label and target.</param>
    /// <returns>A new, mutable HTML page tree whose root contains one <c>main</c> element.</returns>
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

    /// <summary>
    /// Creates the hero section, applying a contrast overlay and foreground color only when an image is present.
    /// </summary>
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

        var foregroundColor = backgroundImageUrl is null ? null : CssColor.Hex("#ffffff");
        hero.Children.Add(TextElement(
            "h1",
            title,
            fontSizeRem: 3m,
            fontWeight: 800,
            centered: true,
            color: foregroundColor));
        hero.Children.Add(TextElement(
            "p",
            summary,
            fontSizeRem: 1.25m,
            lineHeight: 1.6m,
            centered: true,
            color: foregroundColor));
        return hero;
    }

    /// <summary>
    /// Creates a text-only element with the supplied portable typography values.
    /// </summary>
    private static HtmlNode TextElement(
        string tag,
        string text,
        decimal? fontSizeRem = null,
        int? fontWeight = null,
        decimal? lineHeight = null,
        bool centered = false,
        CssColor? color = null)
    {
        var element = HtmlNode.CreateElement(tag);
        element.Style = new HtmlStyle
        {
            Typography = new CssTypographyStyle
            {
                Color = color,
                FontSize = fontSizeRem is null ? null : CssLength.Rem(fontSizeRem.Value),
                FontWeight = fontWeight,
                LineHeight = lineHeight,
                Alignment = centered ? CssTextAlignment.Center : null
            }
        };
        element.Children.Add(HtmlNode.CreateText(text));
        return element;
    }

    /// <summary>
    /// Creates equal logical spacing on all four sides.
    /// </summary>
    private static CssLogicalSpacing AllSides(decimal rem) => new()
    {
        BlockStart = CssLength.Rem(rem),
        InlineEnd = CssLength.Rem(rem),
        BlockEnd = CssLength.Rem(rem),
        InlineStart = CssLength.Rem(rem)
    };
}
