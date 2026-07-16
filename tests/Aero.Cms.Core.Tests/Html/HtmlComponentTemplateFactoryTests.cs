using Aero.Cms.Html;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Core.Tests.Html;

public sealed class HtmlComponentTemplateFactoryTests
{
    private static readonly HtmlElementCatalog Catalog = HtmlElementCatalog.CreateDefault();
    private static readonly HtmlComponentTemplateFactory Factory = new(Catalog);
    private static readonly HtmlContentModelPolicy ContentPolicy = new(Catalog);
    private static readonly HtmlAttributePolicy AttributePolicy = new();
    private static readonly HtmlContentValidator Validator = new(Catalog, ContentPolicy, AttributePolicy);
    private static readonly HtmlStaticRenderer Renderer = new(Catalog, ContentPolicy, AttributePolicy, Validator);

    [Test]
    public async Task Every_component_is_valid_styled_ordinary_html()
    {
        foreach (var kind in Enum.GetValues<HtmlComponentTemplateKind>())
        {
            var created = Factory.Create(kind) as Result<HtmlNode>.Ok;
            await Assert.That(created).IsNotNull();

            var content = new HtmlPageContent();
            content.Root.Children.Add(created!.Value);

            var validation = Validator.Validate(content);
            if (validation is Result<bool>.Failure { Error: AeroError.Validation error })
            {
                throw new InvalidOperationException($"{kind}: {string.Join("; ", error.Errors)}");
            }
            await Assert.That(validation).IsTypeOf<Result<bool>.Ok>();
            var compiled = new NativeCssStyleCompiler().Compile(content, new NativeStyleProfile());
            await Assert.That(compiled).IsTypeOf<Result<CompiledPageStyles>.Ok>();
            await Assert.That(HtmlTreeOperations.HasUniqueNodeIds(created.Value)).IsTrue();
        }
    }

    [Test]
    public async Task Every_component_validates_compiles_and_renders_as_safe_semantic_html()
    {
        foreach (var kind in Enum.GetValues<HtmlComponentTemplateKind>())
        {
            var created = Factory.Create(kind) as Result<HtmlNode>.Ok;
            await Assert.That(created).IsNotNull();

            var content = new HtmlPageContent();
            content.Root.Children.Add(created!.Value);

            await Assert.That(Validator.Validate(content)).IsTypeOf<Result<bool>.Ok>();

            var compiled = new NativeCssStyleCompiler().Compile(content, new NativeStyleProfile())
                as Result<CompiledPageStyles>.Ok;
            await Assert.That(compiled).IsNotNull();

            var rendered = Renderer.RenderPage(content, compiled!.Value)
                as Result<RenderedHtmlPage>.Ok;
            await Assert.That(rendered).IsNotNull();
            await Assert.That(rendered!.Value.Markup).IsNotEmpty();
            await Assert.That(rendered.Value.CssText).IsNotEmpty();
            await Assert.That(rendered.Value.Markup.Contains("<script", StringComparison.OrdinalIgnoreCase)).IsFalse();
            await Assert.That(rendered.Value.Markup.Contains("javascript:", StringComparison.OrdinalIgnoreCase)).IsFalse();
            await Assert.That(
                rendered.Value.Markup.StartsWith("<section", StringComparison.Ordinal)
                || rendered.Value.Markup.StartsWith("<header", StringComparison.Ordinal)
                || rendered.Value.Markup.StartsWith("<aside", StringComparison.Ordinal)).IsTrue();
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
    public async Task Split_hero_and_accordion_faq_supply_distinct_semantic_variants()
    {
        var hero = (Factory.Create(HtmlComponentTemplateKind.SplitHero) as Result<HtmlNode>.Ok)!.Value;
        var faq = (Factory.Create(HtmlComponentTemplateKind.AccordionFaq) as Result<HtmlNode>.Ok)!.Value;

        await Assert.That(hero.Style!.GridColumns).IsEqualTo(2);
        await Assert.That(hero.Style.StackOnSmallScreens).IsTrue();
        await Assert.That(Flatten(hero).Count(node => node.TagName == "img")).IsEqualTo(1);
        await Assert.That(Flatten(hero).Count(node => node.TagName == "a")).IsEqualTo(2);
        await Assert.That(Flatten(faq).Count(node => node.TagName == "details")).IsEqualTo(3);
        await Assert.That(Flatten(faq).Count(node => node.TagName == "summary")).IsEqualTo(3);
    }

    [Test]
    public async Task Feature_list_and_centered_cta_exercise_editable_list_and_background_patterns()
    {
        var features = (Factory.Create(HtmlComponentTemplateKind.FeatureList) as Result<HtmlNode>.Ok)!.Value;
        var callToAction = (Factory.Create(HtmlComponentTemplateKind.CenteredCallToAction) as Result<HtmlNode>.Ok)!.Value;

        await Assert.That(features.Style!.GridColumns).IsEqualTo(2);
        await Assert.That(features.Style.StackOnSmallScreens).IsTrue();
        await Assert.That(Flatten(features).Count(node => node.TagName == "article")).IsEqualTo(3);
        await Assert.That(callToAction.Style!.Surface!.BackgroundImageUrl)
            .IsEqualTo("/_content/Aero.Cms.Shared/images/page-builder/call-to-action.svg");
        await Assert.That(callToAction.Style.Surface.OverlayOpacity).IsEqualTo(0.76m);
        await Assert.That(Flatten(callToAction).Count(node => node.TagName == "a")).IsEqualTo(1);
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

    [Test]
    public async Task Navigation_and_logo_components_supply_accessible_editable_navigation_content()
    {
        var navigation = (Factory.Create(HtmlComponentTemplateKind.NavigationHeader) as Result<HtmlNode>.Ok)!.Value;
        var logos = (Factory.Create(HtmlComponentTemplateKind.LogoCloud) as Result<HtmlNode>.Ok)!.Value;

        var nav = Flatten(navigation).Single(node => node.TagName == "nav");
        var logoList = Flatten(logos).Single(node => node.TagName == "ul");

        await Assert.That(navigation.TagName).IsEqualTo("header");
        await Assert.That(nav.Attributes["aria-label"]).IsEqualTo("Main navigation");
        await Assert.That(Flatten(navigation).Count(node => node.TagName == "a")).IsEqualTo(4);
        await Assert.That(logoList.Style!.Display).IsEqualTo(CssDisplay.Grid);
        await Assert.That(logoList.Style.GridColumns).IsEqualTo(3);
        await Assert.That(logoList.Style.StackOnSmallScreens).IsTrue();
        await Assert.That(logoList.Children.Count(node => node.TagName == "li")).IsEqualTo(6);
        await Assert.That(Flatten(logos).Count(node => node.TagName == "a")).IsEqualTo(6);
        await Assert.That(Flatten(logos).Count(node => node.TagName == "span")).IsEqualTo(6);
    }

    [Test]
    public async Task Pricing_and_team_components_supply_responsive_editable_cards_and_media()
    {
        var pricing = (Factory.Create(HtmlComponentTemplateKind.PricingGrid) as Result<HtmlNode>.Ok)!.Value;
        var team = (Factory.Create(HtmlComponentTemplateKind.TeamGrid) as Result<HtmlNode>.Ok)!.Value;

        var pricingGrid = Flatten(pricing).Single(node => node.Style?.GridColumns == 3);
        var teamGrid = Flatten(team).Single(node => node.Style?.GridColumns == 3);

        await Assert.That(pricingGrid.Style!.StackOnSmallScreens).IsTrue();
        await Assert.That(pricingGrid.Children.Count(node => node.TagName == "article")).IsEqualTo(3);
        await Assert.That(Flatten(pricing).Count(node => node.TagName == "h3")).IsEqualTo(3);
        await Assert.That(Flatten(pricing).Count(node => node.TagName == "ul")).IsEqualTo(3);
        await Assert.That(Flatten(pricing).Count(node => node.TagName == "a")).IsEqualTo(3);
        await Assert.That(teamGrid.Style!.StackOnSmallScreens).IsTrue();
        await Assert.That(teamGrid.Children.Count(node => node.TagName == "article")).IsEqualTo(3);
        await Assert.That(Flatten(team).Count(node => node.TagName == "figure")).IsEqualTo(3);
        await Assert.That(Flatten(team).Count(node => node.TagName == "img")).IsEqualTo(3);
        await Assert.That(Flatten(team).Count(node => node.TagName == "figcaption")).IsEqualTo(3);
    }

    [Test]
    public async Task Page_local_footer_and_static_newsletter_supply_safe_editable_structure()
    {
        var footer = (Factory.Create(HtmlComponentTemplateKind.SiteFooter) as Result<HtmlNode>.Ok)!.Value;
        var newsletter = (Factory.Create(HtmlComponentTemplateKind.NewsletterSignup) as Result<HtmlNode>.Ok)!.Value;
        var footerGrid = Flatten(footer).Single(node => node.Style?.GridColumns == 3);
        var form = Flatten(newsletter).Single(node => node.TagName == "form");
        var input = Flatten(newsletter).Single(node => node.TagName == "input");
        var label = Flatten(newsletter).Single(node => node.TagName == "label");
        var button = Flatten(newsletter).Single(node => node.TagName == "button");

        await Assert.That(footer.TagName).IsEqualTo("section");
        await Assert.That(footerGrid.Style!.StackOnSmallScreens).IsTrue();
        await Assert.That(footerGrid.Children.Count(node => node.TagName == "div")).IsEqualTo(3);
        await Assert.That(Flatten(footer).Count(node => node.TagName == "ul")).IsEqualTo(3);
        await Assert.That(Flatten(footer).Count(node => node.TagName == "a")).IsEqualTo(9);
        await Assert.That(form.Attributes).DoesNotContainKey("action");
        await Assert.That(form.Attributes).DoesNotContainKey("method");
        await Assert.That(input.Attributes["type"]).IsEqualTo("email");
        await Assert.That(input.Attributes["name"]).IsEqualTo("email");
        await Assert.That(label.Attributes["for"]).IsEqualTo(input.Attributes["id"]);
        await Assert.That(button.Attributes["type"]).IsEqualTo("button");
    }

    [Test]
    public async Task Announcement_and_latest_articles_remain_static_responsive_html()
    {
        var announcement = (Factory.Create(HtmlComponentTemplateKind.AnnouncementBanner) as Result<HtmlNode>.Ok)!.Value;
        var articles = (Factory.Create(HtmlComponentTemplateKind.LatestArticles) as Result<HtmlNode>.Ok)!.Value;
        var articleGrid = Flatten(articles).Single(node => node.Style?.GridColumns == 3);

        await Assert.That(announcement.TagName).IsEqualTo("aside");
        await Assert.That(announcement.Style!.Display).IsEqualTo(CssDisplay.Flex);
        await Assert.That(announcement.Style.StackOnSmallScreens).IsTrue();
        await Assert.That(Flatten(announcement).Count(node => node.TagName == "button")).IsEqualTo(0);
        await Assert.That(Flatten(announcement).Count(node => node.TagName == "a")).IsEqualTo(1);
        await Assert.That(articles.TagName).IsEqualTo("section");
        await Assert.That(articleGrid.Style!.StackOnSmallScreens).IsTrue();
        await Assert.That(articleGrid.Children.Count(node => node.TagName == "article")).IsEqualTo(3);
        await Assert.That(Flatten(articles).Count(node => node.TagName == "h3")).IsEqualTo(3);
        await Assert.That(Flatten(articles).Count(node => node.TagName == "a")).IsEqualTo(3);
        await Assert.That(Flatten(articles).Count(node => node.TagName == "img")).IsEqualTo(0);
        await Assert.That(Flatten(articles).Count(node => node.TagName == "time")).IsEqualTo(0);
    }

    [Test]
    public async Task Process_steps_preserve_semantic_ordered_list_structure()
    {
        var process = (Factory.Create(HtmlComponentTemplateKind.ProcessSteps) as Result<HtmlNode>.Ok)!.Value;
        var header = process.Children.Single(node => node.TagName == "header");
        var list = process.Children.Single(node => node.TagName == "ol");

        await Assert.That(process.TagName).IsEqualTo("section");
        await Assert.That(header.Children.Select(node => node.TagName!)).IsEquivalentTo(["h2", "p"]);
        await Assert.That(list.Children.Count(node => node.TagName == "li")).IsEqualTo(3);
        foreach (var item in list.Children)
        {
            var card = item.Children.Single(node => node.TagName == "div");
            await Assert.That(card.Children.Select(node => node.TagName!)).IsEquivalentTo(["span", "h3", "p"]);
        }

        await Assert.That(Flatten(process).Where(node => node.TagName == "span")
            .SelectMany(node => node.Children)
            .Where(node => node.Kind == HtmlNodeKind.Text)
            .Select(node => node.Text!))
            .IsEquivalentTo(new[] { "01", "02", "03" });
    }

    [Test]
    public async Task Showcase_collection_preserves_semantic_linked_cards_in_a_responsive_list_grid()
    {
        var collection = (Factory.Create(HtmlComponentTemplateKind.ShowcaseCollection) as Result<HtmlNode>.Ok)!.Value;
        var header = collection.Children.Single(node => node.TagName == "header");
        var grid = collection.Children.Single(node => node.TagName == "ul");

        await Assert.That(collection.TagName).IsEqualTo("section");
        await Assert.That(header.Children.Select(node => node.TagName!)).IsEquivalentTo(["h2", "p"]);
        await Assert.That(grid.Style!.Display).IsEqualTo(CssDisplay.Grid);
        await Assert.That(grid.Style.GridColumns).IsEqualTo(3);
        await Assert.That(grid.Style.StackOnSmallScreens).IsTrue();
        await Assert.That(grid.Children.Count(node => node.TagName == "li")).IsEqualTo(3);
        await Assert.That(Flatten(collection).Count(node => node.TagName == "article")).IsEqualTo(3);
        await Assert.That(Flatten(collection).Count(node => node.TagName == "figure")).IsEqualTo(3);
        await Assert.That(Flatten(collection).Count(node => node.TagName == "img")).IsEqualTo(3);
        await Assert.That(Flatten(collection).Count(node => node.TagName == "a")).IsEqualTo(9);
    }

    [Test]
    public async Task Timeline_comparison_and_details_templates_preserve_their_semantic_structures()
    {
        var timeline = (Factory.Create(HtmlComponentTemplateKind.MilestoneTimeline) as Result<HtmlNode>.Ok)!.Value;
        var comparison = (Factory.Create(HtmlComponentTemplateKind.FeatureComparisonTable) as Result<HtmlNode>.Ok)!.Value;
        var details = (Factory.Create(HtmlComponentTemplateKind.DetailsList) as Result<HtmlNode>.Ok)!.Value;

        var timelineList = timeline.Children.Single(node => node.TagName == "ol");
        await Assert.That(timelineList.Children.Count(node => node.TagName == "li")).IsEqualTo(3);
        await Assert.That(Flatten(timeline).Count(node => node.TagName == "time")).IsEqualTo(3);
        await Assert.That(Flatten(timeline).Where(node => node.TagName == "time")
            .All(node => node.Attributes.ContainsKey("datetime"))).IsTrue();
        await Assert.That(Flatten(timeline).Count(node => node.TagName == "article")).IsEqualTo(3);

        var table = comparison.Children.Single(node => node.TagName == "table");
        var head = table.Children.Single(node => node.TagName == "thead");
        var body = table.Children.Single(node => node.TagName == "tbody");
        await Assert.That(table.Children.Count(node => node.TagName == "caption")).IsEqualTo(1);
        await Assert.That(Flatten(head).Where(node => node.TagName == "th")
            .Count(node => node.Attributes.GetValueOrDefault("scope") == "col")).IsEqualTo(3);
        await Assert.That(body.Children.Count(node => node.TagName == "tr")).IsEqualTo(4);
        await Assert.That(body.Children.All(row => row.Children.Count == 3
            && row.Children[0].TagName == "th"
            && row.Children[0].Attributes.GetValueOrDefault("scope") == "row"
            && row.Children.Skip(1).All(cell => cell.TagName == "td"))).IsTrue();

        var detailList = details.Children.Single(node => node.TagName == "dl");
        await Assert.That(detailList.Style!.GridColumns).IsEqualTo(2);
        await Assert.That(detailList.Style.StackOnSmallScreens).IsTrue();
        await Assert.That(detailList.Children.Select(node => node.TagName))
            .IsEquivalentTo(["dt", "dd", "dt", "dd", "dt", "dd", "dt", "dd"]);
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
