using Aero.Cms.Abstractions.Blocks.Neo.Styles;

namespace Aero.Cms.Abstractions.Tests;

public sealed class NodeStyleCssRendererTests
{
    [Test]
    public async Task RendersWhitelistedLogicalStyles()
    {
        var style = new NodeStyle
        {
            Width = new CssLength(80, CssLengthUnit.Percent),
            Padding = new LogicalSpacing
            {
                BlockStart = new CssLength(16, CssLengthUnit.Pixels),
                InlineEnd = new CssLength(2, CssLengthUnit.Rem)
            },
            Opacity = 0.75m,
            ForegroundColor = new CssColor("#112233"),
            BackgroundColor = new CssColor("#f8fafc"),
            BackgroundOverlayColor = new CssColor("rgba(15, 23, 42, 0.35)"),
            BackgroundGradient = new LinearGradient
            {
                Angle = 135m,
                StartColor = new CssColor("#112233"),
                EndColor = new CssColor("#abcdef")
            },
            BackgroundImage = new BackgroundImageStyle
            {
                MediaId = 42,
                Url = "/api/media/42",
                Size = BackgroundImageSize.Cover,
                Position = BackgroundImagePosition.BlockStartInlineEnd
            },
            BorderColor = new CssColor("#334155"),
            BorderWidth = new CssLength(1, CssLengthUnit.Pixels),
            BorderRadius = new CssLength(8, CssLengthUnit.Pixels),
            Shadow = new BoxShadow
            {
                OffsetX = 0m,
                OffsetY = 8m,
                Blur = 24m,
                Spread = -4m,
                Color = new CssColor("#00000033")
            },
            FontSize = new CssLength(18, CssLengthUnit.Pixels),
            FontWeight = FontWeight.SemiBold,
            LineHeight = 1.5m,
            LetterSpacing = new CssLength(1, CssLengthUnit.Pixels),
            TextAlignment = TextAlignment.Start,
            Direction = ContentDirection.RightToLeft
        };

        var css = NodeStyleCssRenderer.Render(style);

        await Assert.That(css).Contains("width:80%");
        await Assert.That(css).Contains("padding-block-start:16px");
        await Assert.That(css).Contains("padding-inline-end:2rem");
        await Assert.That(css).Contains("opacity:0.75");
        await Assert.That(css).Contains("color:#112233");
        await Assert.That(css).Contains("background-color:#f8fafc");
        await Assert.That(css)
            .Contains("background-image:linear-gradient(135deg,#112233 0%,#abcdef 100%)");
        await Assert.That(css)
            .Contains("background-image:linear-gradient(135deg,#112233 0%,#abcdef 100%),url(\"/api/media/42\")");
        await Assert.That(css).Contains("background-size:cover");
        await Assert.That(css).Contains("background-position:top left");
        await Assert.That(css).Contains("border-color:#334155");
        await Assert.That(css).Contains("border-width:1px");
        await Assert.That(css).Contains("border-style:solid");
        await Assert.That(css).Contains("border-radius:8px");
        await Assert.That(css).Contains(
            "box-shadow:inset 0 0 0 100vmax rgba(15, 23, 42, 0.35),0px 8px 24px -4px #00000033");
        await Assert.That(css).Contains("font-size:18px");
        await Assert.That(css).Contains("font-weight:600");
        await Assert.That(css).Contains("line-height:1.5");
        await Assert.That(css).Contains("letter-spacing:1px");
        await Assert.That(css).Contains("text-align:start");
        await Assert.That(css).Contains("direction:rtl");
    }

    [Test]
    public async Task RendersRgbaRadialGradientWithConfiguredStops()
    {
        var style = new NodeStyle
        {
            BackgroundGradient = new LinearGradient
            {
                Type = GradientType.Radial,
                RadialShape = RadialGradientShape.Circle,
                RadialPosition = RadialGradientPosition.TopRight,
                StartColor = new CssColor("rgba(255, 0, 0, 0.75)"),
                EndColor = new CssColor("rgba(0, 0, 255, 0.25)"),
                StartPosition = 15m,
                EndPosition = 85m
            }
        };

        var css = NodeStyleCssRenderer.Render(style);

        await Assert.That(css).Contains(
            "background-image:radial-gradient(circle at top right,rgba(255, 0, 0, 0.75) 15%,rgba(0, 0, 255, 0.25) 85%)");
    }

    [Test]
    public async Task RendersRgbColorsEmittedByPicker()
    {
        var style = new NodeStyle
        {
            BackgroundColor = new CssColor("rgb(208, 2, 2)")
        };

        await Assert.That(NodeStyleCssRenderer.Render(style))
            .Contains("background-color:rgb(208, 2, 2)");
    }

    [Test]
    public async Task RejectsUnsafeBackgroundImageUrl()
    {
        var style = new NodeStyle
        {
            BackgroundImage = new BackgroundImageStyle
            {
                Url = "javascript:alert(1)"
            }
        };

        await Assert.That(NodeStyleCssRenderer.Render(style))
            .DoesNotContain("url(");
    }

    [Test]
    [Arguments(BackgroundImageRepeat.NoRepeat, "background-repeat:no-repeat")]
    [Arguments(BackgroundImageRepeat.Repeat, "background-repeat:repeat")]
    [Arguments(BackgroundImageRepeat.RepeatX, "background-repeat:repeat-x")]
    [Arguments(BackgroundImageRepeat.RepeatY, "background-repeat:repeat-y")]
    public async Task RendersTypedBackgroundRepeat(
        BackgroundImageRepeat repeat,
        string expected)
    {
        var style = new NodeStyle
        {
            BackgroundImage = new BackgroundImageStyle
            {
                Url = "/media/pattern.png",
                Repeat = repeat
            }
        };

        await Assert.That(NodeStyleCssRenderer.Render(style)).Contains(expected);
    }

    [Test]
    public async Task ResponsiveOverrideCanDisableInheritedShadow()
    {
        var style = new ResponsiveNodeStyle
        {
            Base = new NodeStyle { Shadow = new BoxShadow() },
            Mobile = new NodeStyleOverride
            {
                Shadow = new BoxShadow { Enabled = false }
            }
        };

        await Assert.That(NodeStyleCssRenderer.Render(style, EditorBreakpoint.Desktop))
            .Contains("box-shadow");
        await Assert.That(NodeStyleCssRenderer.Render(style, EditorBreakpoint.Mobile))
            .DoesNotContain("box-shadow");
    }

    [Test]
    public async Task RejectsInvalidGradientValues()
    {
        var style = new NodeStyle
        {
            BackgroundGradient = new LinearGradient
            {
                Angle = 500m,
                StartColor = new CssColor("#ffffff"),
                EndColor = new CssColor("url(javascript:alert(1))")
            }
        };

        await Assert.That(NodeStyleCssRenderer.Render(style))
            .DoesNotContain("background-image");
    }

    [Test]
    public async Task ResponsiveOverrideCanDisableInheritedGradient()
    {
        var style = new ResponsiveNodeStyle
        {
            Base = new NodeStyle
            {
                BackgroundGradient = new LinearGradient
                {
                    StartColor = new CssColor("#ffffff"),
                    EndColor = new CssColor("#000000")
                }
            },
            Mobile = new NodeStyleOverride
            {
                BackgroundGradient = new LinearGradient { Enabled = false }
            }
        };

        await Assert.That(NodeStyleCssRenderer.Render(style, EditorBreakpoint.Desktop))
            .Contains("background-image");
        await Assert.That(NodeStyleCssRenderer.Render(style, EditorBreakpoint.Mobile))
            .DoesNotContain("background-image");
    }

    [Test]
    public async Task ResolvesResponsiveInheritance()
    {
        var style = new ResponsiveNodeStyle
        {
            Base = new NodeStyle
            {
                Width = new CssLength(100, CssLengthUnit.Percent)
            },
            Mobile = new NodeStyleOverride
            {
                Width = new CssLength(320, CssLengthUnit.Pixels)
            }
        };

        var css = NodeStyleCssRenderer.Render(style, EditorBreakpoint.Mobile);

        await Assert.That(css).Contains("width:320px");
    }

    [Test]
    public async Task RejectsInvalidNumericValues()
    {
        var style = new NodeStyle
        {
            Width = new CssLength(-1, CssLengthUnit.Pixels),
            Height = new CssLength(200_000, CssLengthUnit.Pixels),
            Opacity = 2m,
            ForegroundColor = new CssColor("expression(alert(1))")
        };

        var css = NodeStyleCssRenderer.Render(style);

        await Assert.That(css).IsEmpty();
    }

    [Test]
    public async Task RendersResponsiveVisibility()
    {
        var style = new ResponsiveNodeStyle
        {
            Mobile = new NodeStyleOverride { Hidden = true }
        };

        await Assert.That(NodeStyleCssRenderer.Render(style, EditorBreakpoint.Desktop))
            .DoesNotContain("display:none");
        await Assert.That(NodeStyleCssRenderer.Render(style, EditorBreakpoint.Mobile))
            .Contains("display:none");
    }
}
