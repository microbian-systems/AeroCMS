using Aero.Cms.Html;
using Aero.Core.Railway;

namespace Aero.Cms.Core.Tests.Html;

public sealed class HtmlLayoutStarterFactoryTests
{
    private static readonly HtmlElementCatalog Catalog = HtmlElementCatalog.CreateDefault();
    private static readonly HtmlLayoutStarterFactory Factory = new(Catalog);
    private static readonly HtmlContentModelPolicy ContentPolicy = new(Catalog);
    private static readonly HtmlAttributePolicy AttributePolicy = new();
    private static readonly HtmlStaticRenderer Renderer = new(
        Catalog,
        ContentPolicy,
        AttributePolicy,
        new HtmlContentValidator(Catalog, ContentPolicy, AttributePolicy));

    [Test]
    public async Task Every_layout_starter_compiles_and_renders_as_ordinary_html()
    {
        foreach (var kind in Enum.GetValues<HtmlLayoutStarterKind>())
        {
            var starter = Factory.Create(kind) as Result<HtmlNode>.Ok;
            await Assert.That(starter).IsNotNull();
            var content = new HtmlPageContent();
            content.Root.Children.Add(starter!.Value);
            var compiled = new NativeCssStyleCompiler().Compile(content, new NativeStyleProfile())
                as Result<CompiledPageStyles>.Ok;
            await Assert.That(compiled).IsNotNull();

            var rendered = Renderer.RenderPage(content, compiled!.Value);

            await Assert.That(rendered).IsTypeOf<Result<RenderedHtmlPage>.Ok>();
        }
    }

    [Test]
    public async Task Two_column_and_card_grid_starters_have_fresh_expected_subtrees()
    {
        var first = (Factory.Create(HtmlLayoutStarterKind.TwoColumns) as Result<HtmlNode>.Ok)!.Value;
        var second = (Factory.Create(HtmlLayoutStarterKind.TwoColumns) as Result<HtmlNode>.Ok)!.Value;
        var cardGrid = (Factory.Create(HtmlLayoutStarterKind.CardGrid) as Result<HtmlNode>.Ok)!.Value;

        await Assert.That(first.Children[0].Style!.GridColumns).IsEqualTo(2);
        await Assert.That(first.Children[0].Children).Count().IsEqualTo(2);
        await Assert.That(first.NodeId).IsNotEqualTo(second.NodeId);
        await Assert.That(first.Children[0].NodeId).IsNotEqualTo(second.Children[0].NodeId);
        await Assert.That(cardGrid.Children[0].Children).Count().IsEqualTo(3);
        await Assert.That(cardGrid.Children[0].Children.All(node => node.TagName == "article")).IsTrue();
        await Assert.That(HtmlTreeOperations.HasUniqueNodeIds(cardGrid)).IsTrue();
    }
}
