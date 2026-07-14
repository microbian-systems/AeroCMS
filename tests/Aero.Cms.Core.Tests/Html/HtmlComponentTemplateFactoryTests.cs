using Aero.Cms.Html;
using Aero.Core.Railway;

namespace Aero.Cms.Core.Tests.Html;

public sealed class HtmlComponentTemplateFactoryTests
{
    private static readonly HtmlElementCatalog Catalog = HtmlElementCatalog.CreateDefault();
    private static readonly HtmlComponentTemplateFactory Factory = new(Catalog);
    private static readonly HtmlContentModelPolicy ContentPolicy = new(Catalog);
    private static readonly HtmlAttributePolicy AttributePolicy = new();
    private static readonly HtmlContentValidator Validator = new(Catalog, ContentPolicy, AttributePolicy);

    [Test]
    public async Task Every_component_is_valid_styled_ordinary_html()
    {
        foreach (var kind in Enum.GetValues<HtmlComponentTemplateKind>())
        {
            var created = Factory.Create(kind) as Result<HtmlNode>.Ok;
            await Assert.That(created).IsNotNull();

            var content = new HtmlPageContent();
            content.Root.Children.Add(created!.Value);

            await Assert.That(Validator.Validate(content)).IsTypeOf<Result<bool>.Ok>();
            var compiled = new NativeCssStyleCompiler().Compile(content, new NativeStyleProfile());
            await Assert.That(compiled).IsTypeOf<Result<CompiledPageStyles>.Ok>();
            await Assert.That(HtmlTreeOperations.HasUniqueNodeIds(created.Value)).IsTrue();
        }
    }

    [Test]
    public async Task Factory_creates_fresh_editable_subtrees_for_each_insertion()
    {
        var first = (Factory.Create(HtmlComponentTemplateKind.Hero) as Result<HtmlNode>.Ok)!.Value;
        var second = (Factory.Create(HtmlComponentTemplateKind.Hero) as Result<HtmlNode>.Ok)!.Value;

        var firstIds = Flatten(first).Select(node => node.NodeId).ToHashSet();
        var secondIds = Flatten(second).Select(node => node.NodeId).ToHashSet();

        await Assert.That(first.TagName).IsEqualTo("section");
        await Assert.That(firstIds.Overlaps(secondIds)).IsFalse();
        await Assert.That(Flatten(first).Any(node => node.TagName == "h1")).IsTrue();
        await Assert.That(Flatten(first).Count(node => node.TagName == "a")).IsEqualTo(2);
    }

    [Test]
    public async Task Feature_and_faq_components_supply_responsive_editable_cards()
    {
        var features = (Factory.Create(HtmlComponentTemplateKind.FeatureGrid) as Result<HtmlNode>.Ok)!.Value;
        var faq = (Factory.Create(HtmlComponentTemplateKind.FrequentlyAskedQuestions) as Result<HtmlNode>.Ok)!.Value;

        var featureGrid = Flatten(features).Single(node => node.Style?.GridColumns == 3);
        var faqGrid = Flatten(faq).Single(node => node.Style?.GridColumns == 2);

        await Assert.That(featureGrid.Style!.StackOnSmallScreens).IsTrue();
        await Assert.That(featureGrid.Children.Count(node => node.TagName == "article")).IsEqualTo(3);
        await Assert.That(faqGrid.Style!.StackOnSmallScreens).IsTrue();
        await Assert.That(faqGrid.Children.Count(node => node.TagName == "article")).IsEqualTo(4);
    }

    [Test]
    public async Task Contact_form_supplies_accessible_labels_and_static_controls()
    {
        var component = (Factory.Create(HtmlComponentTemplateKind.ContactForm) as Result<HtmlNode>.Ok)!.Value;
        var nodes = Flatten(component).ToArray();
        var controls = nodes
            .Where(node => node.TagName is "input" or "textarea")
            .ToArray();
        var labels = nodes
            .Where(node => node.TagName == "label")
            .ToArray();

        await Assert.That(controls).Count().IsEqualTo(3);
        await Assert.That(labels).Count().IsEqualTo(3);
        await Assert.That(nodes.Single(node => node.TagName == "form").Attributes).DoesNotContainKey("action");
        await Assert.That(nodes.Single(node => node.TagName == "button").Attributes["type"]).IsEqualTo("submit");

        foreach (var control in controls)
        {
            await Assert.That(control.Attributes).ContainsKey("id");
            await Assert.That(labels.Any(label =>
                label.Attributes.TryGetValue("for", out var target) &&
                target == control.Attributes["id"])).IsTrue();
        }
    }

    [Test]
    public async Task Practical_components_use_responsive_layouts_and_editable_semantic_content()
    {
        var statistics = (Factory.Create(HtmlComponentTemplateKind.Statistics) as Result<HtmlNode>.Ok)!.Value;
        var imageAndText = (Factory.Create(HtmlComponentTemplateKind.ImageAndText) as Result<HtmlNode>.Ok)!.Value;
        var gallery = (Factory.Create(HtmlComponentTemplateKind.Gallery) as Result<HtmlNode>.Ok)!.Value;
        var testimonial = (Factory.Create(HtmlComponentTemplateKind.Testimonial) as Result<HtmlNode>.Ok)!.Value;

        await Assert.That(Flatten(statistics).Count(node => node.TagName == "data")).IsEqualTo(3);
        await Assert.That(Flatten(statistics).Single(node => node.Style?.GridColumns == 3).Style!.StackOnSmallScreens).IsTrue();
        await Assert.That(imageAndText.Style!.StackOnSmallScreens).IsTrue();
        await Assert.That(Flatten(imageAndText).Any(node => node.TagName == "figure")).IsTrue();
        await Assert.That(Flatten(gallery).Count(node => node.TagName == "img")).IsEqualTo(3);
        await Assert.That(Flatten(gallery).Single(node => node.Style?.GridColumns == 3).Style!.StackOnSmallScreens).IsTrue();
        await Assert.That(Flatten(testimonial).Any(node => node.TagName == "blockquote")).IsTrue();
        await Assert.That(Flatten(testimonial).Any(node => node.TagName == "cite")).IsTrue();
    }

    private static IEnumerable<HtmlNode> Flatten(HtmlNode root)
    {
        yield return root;
        foreach (var child in root.Children)
        {
            foreach (var descendant in Flatten(child))
            {
                yield return descendant;
            }
        }
    }
}
