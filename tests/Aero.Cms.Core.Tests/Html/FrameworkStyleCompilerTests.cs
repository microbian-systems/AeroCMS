using Aero.Cms.Html;
using Aero.Core.Railway;

namespace Aero.Cms.Core.Tests.Html;

public sealed class FrameworkStyleCompilerTests
{
    private static readonly NativeStyleProfile Profile = new();

    [Test]
    public async Task Tailwind_adapter_maps_exact_utilities_and_falls_back_for_native_color()
    {
        var content = CreateContent(new HtmlStyle
        {
            Display = CssDisplay.Flex,
            FlexDirection = CssFlexDirection.Row,
            Gap = CssLength.Rem(1),
            Padding = Uniform(CssLength.Rem(1)),
            Typography = new CssTypographyStyle
            {
                Alignment = CssTextAlignment.Center,
                FontWeight = 700,
                Color = CssColor.Hex("#123456")
            }
        });
        var node = content.Root.Children.Single();
        var compiler = new FrameworkStyleCompiler(new TailwindStyleFrameworkAdapter());

        var result = compiler.Compile(content, Profile) as Result<CompiledPageStyles>.Ok;

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.ClassesFor(node.NodeId))
            .IsEquivalentTo(["flex", "flex-row", "gap-4", "p-4", "text-center", "font-bold", result.Value.ClassesFor(node.NodeId).Single(value => value.StartsWith("aero-s-", StringComparison.Ordinal))]);
        await Assert.That(result.Value.CssText).Contains("color: #123456;");
        await Assert.That(result.Value.CssText).DoesNotContain("display:");
        await Assert.That(result.Value.CssText).DoesNotContain("gap:");
        await Assert.That(result.Value.ProfileId).IsEqualTo("aero-native/tailwind");
        await Assert.That(result.Value.ProfileVersion).IsEqualTo("1+1");
    }

    [Test]
    public async Task Unsupported_framework_values_use_scoped_native_css_instead_of_approximate_classes()
    {
        var content = CreateContent(new HtmlStyle
        {
            Display = CssDisplay.Flex,
            Gap = CssLength.Rem(1.1m),
            MinimumHeight = CssLength.ViewportHeight(75)
        });
        var node = content.Root.Children.Single();
        var compiler = new FrameworkStyleCompiler(new TailwindStyleFrameworkAdapter());

        var result = compiler.Compile(content, Profile) as Result<CompiledPageStyles>.Ok;

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.ClassesFor(node.NodeId)).Contains("flex");
        await Assert.That(result.Value.ClassesFor(node.NodeId).Any(value => value.StartsWith("aero-s-", StringComparison.Ordinal))).IsTrue();
        await Assert.That(result.Value.CssText).Contains("gap: 1.1rem;");
        await Assert.That(result.Value.CssText).Contains("min-height: 75vh;");
    }

    [Test]
    public async Task Responsive_layout_uses_site_breakpoint_native_fallback()
    {
        var content = CreateContent(new HtmlStyle
        {
            Display = CssDisplay.Grid,
            GridColumns = 3,
            StackOnSmallScreens = true,
            Gap = CssLength.Rem(1)
        });
        var node = content.Root.Children.Single();
        var compiler = new FrameworkStyleCompiler(new TailwindStyleFrameworkAdapter());

        var result = compiler.Compile(content, Profile) as Result<CompiledPageStyles>.Ok;

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.ClassesFor(node.NodeId).Any(value => value is "grid" or "grid-cols-3" or "gap-4")).IsFalse();
        await Assert.That(result.Value.CssText).Contains("display: grid;");
        await Assert.That(result.Value.CssText).Contains("grid-template-columns: repeat(3, minmax(0, 1fr));");
        await Assert.That(result.Value.CssText).Contains("@media (max-width: 48rem)");
    }

    [Test]
    public async Task Bootstrap_adapter_maps_only_exact_utilities_and_keeps_grid_columns_native()
    {
        var content = CreateContent(new HtmlStyle
        {
            Display = CssDisplay.Grid,
            GridColumns = 2,
            Gap = CssLength.Rem(1),
            Padding = Uniform(CssLength.Rem(1))
        });
        var node = content.Root.Children.Single();
        var compiler = new FrameworkStyleCompiler(new BootstrapStyleFrameworkAdapter());

        var result = compiler.Compile(content, Profile) as Result<CompiledPageStyles>.Ok;

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.ClassesFor(node.NodeId)).Contains("p-3");
        await Assert.That(result.Value.ClassesFor(node.NodeId)).DoesNotContain("d-grid");
        await Assert.That(result.Value.CssText).Contains("display: grid;");
        await Assert.That(result.Value.CssText).Contains("grid-template-columns: repeat(2, minmax(0, 1fr));");
        await Assert.That(result.Value.CssText).Contains("gap: 1rem;");
    }

    [Test]
    public async Task Compilation_is_stable_across_node_ids_and_rejects_invalid_residual_intent()
    {
        var original = CreateContent(new HtmlStyle
        {
            Display = CssDisplay.Flex,
            Gap = CssLength.Rem(1),
            Typography = new CssTypographyStyle { Color = CssColor.Hex("#abcdef") }
        });
        var duplicate = HtmlTreeOperations.ClonePreservingNodeIds(original);
        duplicate.Root = HtmlTreeOperations.CloneWithFreshNodeIds(original.Root);
        var compiler = new FrameworkStyleCompiler(new TailwindStyleFrameworkAdapter());

        var first = compiler.Compile(original, Profile) as Result<CompiledPageStyles>.Ok;
        var second = compiler.Compile(duplicate, Profile) as Result<CompiledPageStyles>.Ok;

        await Assert.That(first).IsNotNull();
        await Assert.That(second).IsNotNull();
        await Assert.That(second!.Value.ContentHash).IsEqualTo(first!.Value.ContentHash);

        original.Root.Children[0].Style!.GridColumns = 13;
        await Assert.That(compiler.Compile(original, Profile))
            .IsTypeOf<Result<CompiledPageStyles>.Failure>();
    }

    private static HtmlPageContent CreateContent(HtmlStyle style)
    {
        var content = new HtmlPageContent();
        var section = HtmlNode.CreateElement("section");
        section.Style = style;
        content.Root.Children.Add(section);
        return content;
    }

    private static CssLogicalSpacing Uniform(CssLength value) => new()
    {
        BlockStart = new CssLength { Value = value.Value, Unit = value.Unit },
        InlineEnd = new CssLength { Value = value.Value, Unit = value.Unit },
        BlockEnd = new CssLength { Value = value.Value, Unit = value.Unit },
        InlineStart = new CssLength { Value = value.Value, Unit = value.Unit }
    };
}
