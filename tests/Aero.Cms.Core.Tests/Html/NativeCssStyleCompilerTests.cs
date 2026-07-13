using Aero.Cms.Html;
using Aero.Core.Railway;

namespace Aero.Cms.Core.Tests.Html;

public sealed class NativeCssStyleCompilerTests
{
    private static readonly NativeStyleProfile Profile = new()
    {
        ColorTokens = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["surface.brand"] = "#123456"
        }
    };
    private static readonly NativeCssStyleCompiler Compiler = new();

    [Test]
    public async Task Compile_deduplicates_identical_layout_rules_and_uses_logical_properties()
    {
        var content = new HtmlPageContent();
        var first = CreateTwoColumnLayout();
        var second = HtmlTreeOperations.CloneWithFreshNodeIds(first);
        content.Root.Children.AddRange([first, second]);

        var result = Compiler.Compile(content, Profile);

        var compiled = result as Result<CompiledPageStyles>.Ok;
        await Assert.That(compiled).IsNotNull();
        var firstClass = compiled!.Value.ClassesFor(first.NodeId).Single();
        var secondClass = compiled.Value.ClassesFor(second.NodeId).Single();
        await Assert.That(secondClass).IsEqualTo(firstClass);
        await Assert.That(Occurrences(compiled.Value.CssText, "display: grid;")).IsEqualTo(1);
        await Assert.That(compiled.Value.CssText).Contains("grid-template-columns: repeat(2, minmax(0, 1fr));");
        await Assert.That(compiled.Value.CssText).Contains("padding-inline-start: 2rem;");
        await Assert.That(compiled.Value.CssText).Contains("@media (max-width: 48rem)");
        await Assert.That(compiled.Value.ContentHash).Length().IsEqualTo(64);
    }

    [Test]
    public async Task Compile_is_stable_across_editor_node_id_changes_and_rejects_invalid_grid_columns()
    {
        var original = new HtmlPageContent();
        original.Root.Children.Add(CreateTwoColumnLayout());
        var duplicated = new HtmlPageContent
        {
            Root = HtmlTreeOperations.CloneWithFreshNodeIds(original.Root)
        };

        var first = Compiler.Compile(original, Profile) as Result<CompiledPageStyles>.Ok;
        var second = Compiler.Compile(duplicated, Profile) as Result<CompiledPageStyles>.Ok;

        await Assert.That(first).IsNotNull();
        await Assert.That(second).IsNotNull();
        await Assert.That(second!.Value.CssText).IsEqualTo(first!.Value.CssText);
        await Assert.That(second.Value.ContentHash).IsEqualTo(first.Value.ContentHash);

        var invalidStyle = original.Root.Children[0].Style!;
        invalidStyle.GridColumns = 13;
        var invalid = Compiler.Compile(original, Profile);

        await Assert.That(invalid).IsTypeOf<Result<CompiledPageStyles>.Failure>();

        invalidStyle.GridColumns = 2;
        invalidStyle.Gap = new CssLength { Value = 1, Unit = (CssLengthUnit)999 };

        await Assert.That(Compiler.Compile(original, Profile))
            .IsTypeOf<Result<CompiledPageStyles>.Failure>();
    }

    [Test]
    public async Task Compile_emits_safe_surface_styles_and_resolves_profile_color_tokens()
    {
        var content = new HtmlPageContent();
        var section = HtmlNode.CreateElement("section");
        section.Style = new HtmlStyle
        {
            Surface = new CssSurfaceStyle
            {
                BackgroundColor = CssColor.Token("surface.brand"),
                BackgroundImageUrl = "/media/hero image.jpg",
                OverlayColor = CssColor.Hex("#000"),
                OverlayOpacity = 0.4m,
                BackgroundFit = CssBackgroundFit.Cover,
                BackgroundPosition = CssBackgroundPosition.Center,
                BackgroundRepeat = CssBackgroundRepeat.NoRepeat,
                BorderRadius = CssLength.Rem(1)
            }
        };
        content.Root.Children.Add(section);

        var result = Compiler.Compile(content, Profile) as Result<CompiledPageStyles>.Ok;

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.CssText).Contains("background-color: #123456;");
        await Assert.That(result.Value.CssText).Contains("linear-gradient(rgba(0, 0, 0, 0.4), rgba(0, 0, 0, 0.4)), url(\"/media/hero image.jpg\")");
        await Assert.That(result.Value.CssText).Contains("background-size: cover;");
        await Assert.That(result.Value.CssText).Contains("border-radius: 1rem;");
    }

    [Test]
    public async Task Compile_rejects_unknown_tokens_unsafe_media_urls_and_incomplete_overlays()
    {
        var content = new HtmlPageContent();
        var section = HtmlNode.CreateElement("section");
        section.Style = new HtmlStyle
        {
            Surface = new CssSurfaceStyle
            {
                BackgroundColor = CssColor.Token("missing"),
                BackgroundImageUrl = "javascript:alert(1)",
                OverlayColor = CssColor.Hex("#000")
            }
        };
        content.Root.Children.Add(section);

        await Assert.That(Compiler.Compile(content, Profile))
            .IsTypeOf<Result<CompiledPageStyles>.Failure>();
    }

    [Test]
    public async Task Compile_emits_typography_and_gradient_text_without_framework_classes()
    {
        var content = new HtmlPageContent();
        var heading = HtmlNode.CreateElement("h1");
        heading.Style = new HtmlStyle
        {
            Typography = new CssTypographyStyle
            {
                FontSize = CssLength.Rem(3),
                FontWeight = 700,
                LineHeight = 1.1m,
                LetterSpacing = CssLength.Em(-0.02m),
                Alignment = CssTextAlignment.Center,
                Gradient = new CssTextGradient
                {
                    StartColor = CssColor.Token("surface.brand"),
                    EndColor = CssColor.Hex("#abcdef"),
                    AngleDegrees = 135
                }
            }
        };
        content.Root.Children.Add(heading);

        var result = Compiler.Compile(content, Profile) as Result<CompiledPageStyles>.Ok;

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.CssText).Contains("font-size: 3rem;");
        await Assert.That(result.Value.CssText).Contains("font-weight: 700;");
        await Assert.That(result.Value.CssText).Contains("letter-spacing: -0.02em;");
        await Assert.That(result.Value.CssText).Contains("text-align: center;");
        await Assert.That(result.Value.CssText).Contains("linear-gradient(135deg, #123456, #abcdef)");
        await Assert.That(result.Value.CssText).Contains("background-clip: text;");
        await Assert.That(result.Value.CssText).Contains("color: transparent;");
    }

    [Test]
    public async Task Compile_rejects_invalid_typography_intent()
    {
        var content = new HtmlPageContent();
        var paragraph = HtmlNode.CreateElement("p");
        paragraph.Style = new HtmlStyle
        {
            Typography = new CssTypographyStyle
            {
                Color = CssColor.Hex("#123456"),
                FontWeight = 725,
                LineHeight = 0,
                Gradient = new CssTextGradient { AngleDegrees = 361 }
            }
        };
        content.Root.Children.Add(paragraph);

        await Assert.That(Compiler.Compile(content, Profile))
            .IsTypeOf<Result<CompiledPageStyles>.Failure>();

        paragraph.Style.Surface = new CssSurfaceStyle { BackgroundImageUrl = "/media/hero.jpg" };
        paragraph.Style.Typography = new CssTypographyStyle
        {
            Gradient = new CssTextGradient()
        };

        await Assert.That(Compiler.Compile(content, Profile))
            .IsTypeOf<Result<CompiledPageStyles>.Failure>();
    }

    private static HtmlNode CreateTwoColumnLayout() => new()
    {
        Kind = HtmlNodeKind.Element,
        TagName = "section",
        Style = new HtmlStyle
        {
            Display = CssDisplay.Grid,
            GridColumns = 2,
            StackOnSmallScreens = true,
            Gap = CssLength.Rem(1.5m),
            Padding = new CssLogicalSpacing { InlineStart = CssLength.Rem(2) }
        }
    };

    private static int Occurrences(string value, string fragment) =>
        value.Split(fragment, StringSplitOptions.None).Length - 1;
}
