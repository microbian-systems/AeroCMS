using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Html;

/// <summary>
/// Creates curated components as ordinary, independently editable HTML nodes.
/// </summary>
public sealed class HtmlComponentCatalog
{
    private readonly HtmlElementCatalog catalog;
    private readonly IReadOnlyList<HtmlComponentDescriptor> _all;
    private readonly IReadOnlyDictionary<string, HtmlComponentDescriptor> _byKey;
    private readonly IReadOnlyList<HtmlComponentDescriptor> _basics;
    private readonly IReadOnlyList<HtmlComponentDescriptor> _daisy;
    private static readonly IReadOnlyList<HtmlComponentDescriptor> NoPatterns =
        Array.AsReadOnly(Array.Empty<HtmlComponentDescriptor>());
    private const string PlaceholderBasePath = "/_content/Aero.Cms.Shared/images/page-builder";

    public HtmlComponentCatalog(HtmlElementCatalog catalog)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _all =
        [
        Descriptor("basic.hero", "Hero", "A centered introduction with primary actions.", HtmlComponentCatalogGroup.Basics, "◆", "section", ["introduction", "actions"], CreateHero),
        Descriptor("basic.split-hero", "Hero + image", "A responsive split hero with editable image and actions.", HtmlComponentCatalogGroup.Basics, "◩", "section", ["introduction", "media"], CreateSplitHero),
        Descriptor("basic.feature-grid", "Features", "A responsive three-card feature section.", HtmlComponentCatalogGroup.Basics, "▦", "section", ["cards", "benefits"], CreateFeatureGrid),
        Descriptor("basic.feature-list", "Feature list", "A responsive numbered benefit list.", HtmlComponentCatalogGroup.Basics, "☷", "section", ["benefits", "numbered"], CreateFeatureList),
        Descriptor("basic.call-to-action", "Call to action", "A focused prompt with one primary action.", HtmlComponentCatalogGroup.Basics, "→", "section", ["conversion", "cta"], CreateCallToAction),
        Descriptor("basic.centered-call-to-action", "CTA + image", "A centered call to action over an editable background image.", HtmlComponentCatalogGroup.Basics, "◎", "section", ["conversion", "cta", "media"], CreateCenteredCallToAction),
        Descriptor("basic.faq", "FAQ", "A responsive question-and-answer section.", HtmlComponentCatalogGroup.Basics, "?", "section", ["questions", "answers"], CreateFrequentlyAskedQuestions),
        Descriptor("basic.accordion-faq", "FAQ accordion", "Expandable semantic questions and answers.", HtmlComponentCatalogGroup.Basics, "⌄", "section", ["questions", "details"], CreateAccordionFaq),
        Descriptor("basic.testimonial", "Testimonial", "A highlighted customer quotation.", HtmlComponentCatalogGroup.Basics, "“”", "section", ["quote", "trust"], CreateTestimonial),
        Descriptor("basic.statistics", "Statistics", "Three responsive headline metrics.", HtmlComponentCatalogGroup.Basics, "%", "section", ["metrics", "trust"], CreateStatistics),
        Descriptor("basic.image-and-text", "Image + text", "A responsive visual and copy split.", HtmlComponentCatalogGroup.Basics, "◫", "section", ["media", "copy"], CreateImageAndText),
        Descriptor("basic.contact-form", "Contact form", "A static, accessible contact section.", HtmlComponentCatalogGroup.Basics, "✉", "section", ["form", "contact"], CreateContactForm),
        Descriptor("basic.gallery", "Gallery", "A responsive three-image gallery.", HtmlComponentCatalogGroup.Basics, "▧", "section", ["images", "media"], CreateGallery),
        Descriptor("basic.navigation", "Navigation", "A responsive site header with editable links.", HtmlComponentCatalogGroup.Basics, "☰", "header", ["links", "header"], CreateNavigationHeader),
        Descriptor("basic.logo-cloud", "Partner logos", "An accessible grid of editable partner names.", HtmlComponentCatalogGroup.Basics, "✦", "section", ["logos", "partners"], CreateLogoCloud),
        Descriptor("basic.pricing-grid", "Pricing", "Three responsive plans with benefits and actions.", HtmlComponentCatalogGroup.Basics, "¤", "section", ["plans", "conversion"], CreatePricingGrid),
        Descriptor("basic.team-grid", "Team", "A responsive team section with editable portraits.", HtmlComponentCatalogGroup.Basics, "♙", "section", ["people", "portraits"], CreateTeamGrid),
        Descriptor("basic.footer", "Footer links", "A responsive page section of editable link groups.", HtmlComponentCatalogGroup.Basics, "▤", "section", ["links", "navigation"], CreateSiteFooter),
        Descriptor("basic.newsletter", "Newsletter", "A static email signup section.", HtmlComponentCatalogGroup.Basics, "✉", "section", ["email", "form"], CreateNewsletterSignup),
        Descriptor("basic.announcement", "Announcement", "A responsive update banner with one ordinary link.", HtmlComponentCatalogGroup.Basics, "!", "aside", ["banner", "notice"], CreateAnnouncementBanner),
        Descriptor("basic.latest-articles", "Latest articles", "Three responsive static article cards.", HtmlComponentCatalogGroup.Basics, "▥", "section", ["articles", "cards"], CreateLatestArticles),
        Descriptor("basic.process-steps", "Process steps", "Three numbered steps with editable explanations.", HtmlComponentCatalogGroup.Basics, "①", "section", ["steps", "process"], CreateProcessSteps),
        Descriptor("basic.collection", "Collection", "A responsive three-item static collection.", HtmlComponentCatalogGroup.Basics, "▦", "section", ["cards", "collection"], CreateShowcaseCollection),
        Descriptor("basic.milestone-timeline", "Timeline", "Three dated milestones in a semantic ordered timeline.", HtmlComponentCatalogGroup.Basics, "◷", "section", ["dates", "milestones"], CreateMilestoneTimeline),
        Descriptor("basic.feature-comparison", "Comparison table", "A compact editable feature comparison.", HtmlComponentCatalogGroup.Basics, "▤", "section", ["table", "comparison"], CreateFeatureComparisonTable),
        Descriptor("basic.details-list", "Details list", "Responsive editable terms and descriptions.", HtmlComponentCatalogGroup.Basics, "☷", "section", ["terms", "definitions"], CreateDetailsList),
        Descriptor("basic.confirmation-dialog", "Confirmation dialog", "An editable open dialog with two static actions.", HtmlComponentCatalogGroup.Basics, "▣", "section", ["dialog", "confirmation"], CreateConfirmationDialog),
        Descriptor("daisy.button", "Button", "A prominent action button.", HtmlComponentCatalogGroup.Daisy, "●", "button", ["action", "primary"], CreateDaisyButton),
        Descriptor("daisy.badge", "Badge", "A compact status label.", HtmlComponentCatalogGroup.Daisy, "●", "span", ["label", "status"], CreateDaisyBadge),
        Descriptor("daisy.alert", "Alert", "An accessible status message.", HtmlComponentCatalogGroup.Daisy, "!", "div", ["notice", "message"], CreateDaisyAlert),
        Descriptor("daisy.card", "Card", "A content card with a clear action.", HtmlComponentCatalogGroup.Daisy, "▣", "article", ["content", "panel"], CreateDaisyCard),
        Descriptor("daisy.hero", "Hero", "A full-width Daisy introduction.", HtmlComponentCatalogGroup.Daisy, "◆", "section", ["introduction", "banner"], CreateDaisyHero),
        Descriptor("daisy.stat", "Stat", "A concise headline metric.", HtmlComponentCatalogGroup.Daisy, "%", "section", ["metric", "statistic"], CreateDaisyStat),
        Descriptor("daisy.progress", "Progress", "A native progress indicator.", HtmlComponentCatalogGroup.Daisy, "━", "progress", ["loading", "completion"], CreateDaisyProgress),
        Descriptor("daisy.skeleton", "Skeleton", "A content placeholder.", HtmlComponentCatalogGroup.Daisy, "░", "div", ["loading", "placeholder"], CreateDaisySkeleton),
        Descriptor("daisy.divider", "Divider", "A semantic visual separator.", HtmlComponentCatalogGroup.Daisy, "—", "div", ["separator", "section"], CreateDaisyDivider),
        Descriptor("daisy.breadcrumbs", "Breadcrumbs", "A navigational trail.", HtmlComponentCatalogGroup.Daisy, "›", "nav", ["navigation", "links"], CreateDaisyBreadcrumbs),
        Descriptor("daisy.steps", "Steps", "A three-step process.", HtmlComponentCatalogGroup.Daisy, "①", "ul", ["process", "ordered"], CreateDaisySteps),
        Descriptor("daisy.timeline", "Timeline", "A semantic milestone timeline.", HtmlComponentCatalogGroup.Daisy, "◷", "ul", ["milestones", "dates"], CreateDaisyTimeline),
        Descriptor("daisy.table", "Table", "A structured comparison table.", HtmlComponentCatalogGroup.Daisy, "▤", "table", ["data", "comparison"], CreateDaisyTable),
        Descriptor("daisy.pagination", "Pagination", "A page navigation control.", HtmlComponentCatalogGroup.Daisy, "»", "nav", ["navigation", "pages"], CreateDaisyPagination),
        Descriptor("daisy.accordion", "Accordion", "A script-free native details disclosure.", HtmlComponentCatalogGroup.Daisy, "⌄", "details", ["questions", "disclosure"], CreateDaisyAccordion),
        ];
        _all = Array.AsReadOnly(_all.ToArray());
        _byKey = _all.ToDictionary(descriptor => descriptor.Key, StringComparer.Ordinal);
        _basics = Array.AsReadOnly(_all.Where(descriptor => descriptor.Group == HtmlComponentCatalogGroup.Basics).ToArray());
        _daisy = Array.AsReadOnly(_all.Where(descriptor => descriptor.Group == HtmlComponentCatalogGroup.Daisy).ToArray());
    }

    /// <summary>Gets every descriptor in deterministic registration order.</summary>
    public IReadOnlyList<HtmlComponentDescriptor> All => _all;

    public IReadOnlyList<HtmlComponentDescriptor> Basics => _basics;
    public IReadOnlyList<HtmlComponentDescriptor> Daisy => _daisy;
    public IReadOnlyList<HtmlComponentDescriptor> Patterns => NoPatterns;

    public bool TryGet(string? key, out HtmlComponentDescriptor? descriptor)
    {
        if (!string.IsNullOrWhiteSpace(key) && _byKey.TryGetValue(key, out var found))
        {
            descriptor = found;
            return true;
        }

        descriptor = null;
        return false;
    }

    public Result<HtmlNode> Create(string? key) => TryGet(key, out var descriptor)
        ? descriptor!.Create()
        : AeroError.ValidationError(["The requested component key is not supported."]);

    private static HtmlComponentDescriptor Descriptor(string key, string displayName, string description, HtmlComponentCatalogGroup group, string icon, string rootTagName, IReadOnlyList<string> keywords, Func<HtmlNode> create) =>
        new(key, displayName, description, group, icon, rootTagName, Array.AsReadOnly(keywords.ToArray()), () => new Result<HtmlNode>.Ok(create()));


    /// <summary>Builds a centered introductory section with editable copy, action, and media nodes.</summary>
    private HtmlNode CreateHero()
    {
        var section = Element("section", style: new HtmlStyle
        {
            Display = CssDisplay.Flex,
            AlignItems = CssAlignment.Center,
            JustifyContent = CssJustification.Center,
            MinimumHeight = CssLength.ViewportHeight(65),
            Padding = AllSpacing(CssLength.Rem(2)),
            Surface = Surface("#111827")
        });
        var content = Element("div", style: new HtmlStyle
        {
            Display = CssDisplay.Flex,
            FlexDirection = CssFlexDirection.Column,
            AlignItems = CssAlignment.Center,
            Gap = CssLength.Rem(1.5m)
        });
        content.Children.Add(Element("h1", "Build something remarkable", Typography(
            color: "#ffffff", size: CssLength.Rem(3), weight: 800, alignment: CssTextAlignment.Center)));
        content.Children.Add(Element("p", "Create a clear, compelling introduction for your visitors.", Typography(
            color: "#e5e7eb", size: CssLength.Rem(1.125m), alignment: CssTextAlignment.Center)));

        var actions = Element("div", style: new HtmlStyle
        {
            Display = CssDisplay.Flex,
            FlexDirection = CssFlexDirection.Row,
            StackOnSmallScreens = true,
            JustifyContent = CssJustification.Center,
            Gap = CssLength.Rem(0.75m)
        });
        actions.Children.Add(Link("Get started", "#", "#7c3aed", "#ffffff"));
        actions.Children.Add(Link("Learn more", "#", "#ffffff", "#111827"));
        content.Children.Add(actions);
        section.Children.Add(content);
        return section;
    }

    /// <summary>Builds a responsive three-item feature grid with placeholder authoring content.</summary>
    private HtmlNode CreateFeatureGrid()
    {
        var section = Element("section", style: new HtmlStyle
        {
            Padding = AllSpacing(CssLength.Rem(3)),
            Surface = Surface("#ffffff")
        });
        section.Children.Add(Element("h2", "Everything you need", Typography(
            color: "#111827", size: CssLength.Rem(2.25m), weight: 700, alignment: CssTextAlignment.Center)));
        section.Children.Add(Element("p", "Introduce the benefits that make your product or organization stand out.", Typography(
            color: "#4b5563", size: CssLength.Rem(1.05m), alignment: CssTextAlignment.Center)));

        var grid = Element("div", style: new HtmlStyle
        {
            Display = CssDisplay.Grid,
            GridColumns = 3,
            StackOnSmallScreens = true,
            Gap = CssLength.Rem(1.5m),
            Margin = Vertical(CssLength.Rem(2))
        });
        grid.Children.Add(Feature("Fast to customize", "Shape every section with focused visual controls."));
        grid.Children.Add(Feature("Built for your content", "Use semantic HTML without exposing technical complexity."));
        grid.Children.Add(Feature("Ready for every screen", "Responsive layouts keep the experience clear and usable."));
        section.Children.Add(grid);
        return section;
    }

    /// <summary>Builds a responsive copy-and-media hero whose children remain independently editable.</summary>
    private HtmlNode CreateSplitHero()
    {
        var section = Element("section", style: new HtmlStyle
        {
            Display = CssDisplay.Grid,
            GridColumns = 2,
            StackOnSmallScreens = true,
            AlignItems = CssAlignment.Center,
            Gap = CssLength.Rem(3),
            Padding = AllSpacing(CssLength.Rem(2)),
            Surface = Surface("#eff6ff", CssLength.Rem(1))
        });

        var copy = Element("div", style: new HtmlStyle
        {
            Display = CssDisplay.Flex,
            FlexDirection = CssFlexDirection.Column,
            Gap = CssLength.Rem(1.25m)
        });
        copy.Children.Add(Element("p", "A clear reason to choose you", Typography(
            color: "#1d4ed8", size: CssLength.Rem(0.95m), weight: 700)));
        copy.Children.Add(Element("h1", "Make a stronger first impression", Typography(
            color: "#111827", size: CssLength.Rem(2.25m), weight: 800)));
        copy.Children.Add(Element("p", "Pair a focused message with an image that immediately communicates your value.", Typography(
            color: "#4b5563", size: CssLength.Rem(1.125m))));

        var actions = Element("div", style: new HtmlStyle
        {
            Display = CssDisplay.Flex,
            FlexDirection = CssFlexDirection.Row,
            StackOnSmallScreens = true,
            Gap = CssLength.Rem(0.75m)
        });
        actions.Children.Add(Link("Get started", "#", "#2563eb", "#ffffff"));
        actions.Children.Add(Link("See how it works", "#", "#ffffff", "#1e3a8a"));
        copy.Children.Add(actions);

        var media = Element("div", style: new HtmlStyle
        {
            Surface = Surface("#dbeafe", CssLength.Rem(1))
        });
        var figure = Element("figure");
        figure.Children.Add(Image($"{PlaceholderBasePath}/hero.svg", "Describe the main hero image"));
        figure.Children.Add(Element("figcaption", "Replace this image and description with your own."));
        media.Children.Add(figure);

        section.Children.Add(copy);
        section.Children.Add(media);
        return section;
    }

    /// <summary>Builds a compact action section with constrained semantic styling.</summary>
    private HtmlNode CreateCallToAction()
    {
        var section = Element("section", style: new HtmlStyle
        {
            Display = CssDisplay.Flex,
            FlexDirection = CssFlexDirection.Row,
            StackOnSmallScreens = true,
            AlignItems = CssAlignment.Center,
            JustifyContent = CssJustification.SpaceBetween,
            Gap = CssLength.Rem(2),
            Padding = AllSpacing(CssLength.Rem(2.5m)),
            Surface = Surface("#5b21b6", CssLength.Rem(1))
        });
        var copy = Element("div");
        copy.Children.Add(Element("h2", "Ready to take the next step?", Typography(
            color: "#ffffff", size: CssLength.Rem(2), weight: 700)));
        copy.Children.Add(Element("p", "Give visitors one clear action and a reason to act now.", Typography(
            color: "#ede9fe", size: CssLength.Rem(1.05m))));
        section.Children.Add(copy);
        section.Children.Add(Link("Get started", "#", "#ffffff", "#5b21b6"));
        return section;
    }

    /// <summary>Builds an ordered visual feature list without introducing a custom rendering abstraction.</summary>
    private HtmlNode CreateFeatureList()
    {
        var section = Element("section", style: new HtmlStyle
        {
            Display = CssDisplay.Grid,
            GridColumns = 2,
            StackOnSmallScreens = true,
            AlignItems = CssAlignment.Center,
            Gap = CssLength.Rem(3),
            Padding = AllSpacing(CssLength.Rem(3))
        });

        var introduction = Element("div", style: new HtmlStyle
        {
            Display = CssDisplay.Flex,
            FlexDirection = CssFlexDirection.Column,
            Gap = CssLength.Rem(1.25m)
        });
        introduction.Children.Add(Element("h2", "A better way to move forward", Typography(
            color: "#111827", size: CssLength.Rem(2.25m), weight: 700)));
        introduction.Children.Add(Element("p", "Use a short list when each benefit deserves a little more explanation.", Typography(
            color: "#4b5563", size: CssLength.Rem(1.05m))));
        introduction.Children.Add(Link("Explore the details", "#", "#7c3aed", "#ffffff"));

        var features = Element("div", style: new HtmlStyle
        {
            Display = CssDisplay.Flex,
            FlexDirection = CssFlexDirection.Column,
            Gap = CssLength.Rem(1)
        });
        features.Children.Add(FeatureListItem("01", "Start with clarity", "Lead with the outcome visitors care about most."));
        features.Children.Add(FeatureListItem("02", "Build confidence", "Support the message with a concise, useful explanation."));
        features.Children.Add(FeatureListItem("03", "Make action obvious", "Finish with one clear and relevant next step."));

        section.Children.Add(introduction);
        section.Children.Add(features);
        return section;
    }

    /// <summary>Builds a centered action section with ordinary heading, copy, and link nodes.</summary>
    private HtmlNode CreateCenteredCallToAction()
    {
        var section = Element("section", style: new HtmlStyle
        {
            Display = CssDisplay.Flex,
            FlexDirection = CssFlexDirection.Column,
            AlignItems = CssAlignment.Center,
            JustifyContent = CssJustification.Center,
            Gap = CssLength.Rem(1.25m),
            MinimumHeight = CssLength.Rem(24),
            Padding = AllSpacing(CssLength.Rem(3)),
            Surface = new CssSurfaceStyle
            {
                BackgroundImageUrl = $"{PlaceholderBasePath}/call-to-action.svg",
                OverlayColor = CssColor.Hex("#111827"),
                OverlayOpacity = 0.76m,
                BackgroundFit = CssBackgroundFit.Cover,
                BackgroundPosition = CssBackgroundPosition.Center,
                BackgroundRepeat = CssBackgroundRepeat.NoRepeat,
                BorderRadius = CssLength.Rem(1)
            }
        });
        section.Children.Add(Element("h2", "Turn interest into action", Typography(
            color: "#ffffff", size: CssLength.Rem(2.5m), weight: 800, alignment: CssTextAlignment.Center)));
        section.Children.Add(Element("p", "Keep the message focused and give visitors one confident next step.", Typography(
            color: "#e5e7eb", size: CssLength.Rem(1.125m), alignment: CssTextAlignment.Center)));
        section.Children.Add(Link("Start now", "#", "#ffffff", "#111827"));
        return section;
    }

    /// <summary>Builds a static question-and-answer section suitable for direct author editing.</summary>
    private HtmlNode CreateFrequentlyAskedQuestions()
    {
        var section = Element("section", style: new HtmlStyle
        {
            Padding = AllSpacing(CssLength.Rem(3))
        });
        section.Children.Add(Element("h2", "Frequently asked questions", Typography(
            color: "#111827", size: CssLength.Rem(2.25m), weight: 700, alignment: CssTextAlignment.Center)));

        var questions = Element("div", style: new HtmlStyle
        {
            Display = CssDisplay.Grid,
            GridColumns = 2,
            StackOnSmallScreens = true,
            Gap = CssLength.Rem(1.5m),
            Margin = Vertical(CssLength.Rem(2))
        });
        questions.Children.Add(Question("What can I customize?", "Every heading, paragraph, link, layout, and style remains editable."));
        questions.Children.Add(Question("Will it work on mobile?", "The starter layout stacks cleanly on smaller screens."));
        questions.Children.Add(Question("Can I reorder the questions?", "Yes. Select any question card and move it with drag and drop."));
        questions.Children.Add(Question("Can I add more content?", "Add ordinary HTML elements or duplicate the pattern with the editor."));
        section.Children.Add(questions);
        return section;
    }

    /// <summary>Builds a quotation section using semantic quote and attribution elements.</summary>
    private HtmlNode CreateTestimonial()
    {
        var section = Element("section", style: new HtmlStyle
        {
            Padding = AllSpacing(CssLength.Rem(3)),
            Surface = Surface("#f8fafc", CssLength.Rem(1))
        });
        section.Children.Add(Element("h2", "What our customers say", Typography(
            color: "#111827", size: CssLength.Rem(2.25m), weight: 700, alignment: CssTextAlignment.Center)));

        var quote = Element("blockquote", style: new HtmlStyle
        {
            Margin = Vertical(CssLength.Rem(2)),
            Padding = AllSpacing(CssLength.Rem(2)),
            Surface = Surface("#ffffff", CssLength.Rem(0.75m)),
            Typography = new CssTypographyStyle
            {
                Color = CssColor.Hex("#1f2937"),
                FontSize = CssLength.Rem(1.25m),
                LineHeight = 1.6m,
                Alignment = CssTextAlignment.Center
            }
        });
        quote.Children.Add(Element("p", "“This made it easy to turn our ideas into a page our customers understand.”"));
        var attribution = Element("p", style: Typography(
            color: "#6b7280", size: CssLength.Rem(0.95m), weight: 600, alignment: CssTextAlignment.Center));
        attribution.Children.Add(Element("cite", "Customer name"));
        quote.Children.Add(attribution);
        section.Children.Add(quote);
        return section;
    }

    /// <summary>Builds a no-script accordion from native details and summary elements.</summary>
    private HtmlNode CreateAccordionFaq()
    {
        var section = Element("section", style: new HtmlStyle
        {
            Display = CssDisplay.Grid,
            GridColumns = 2,
            StackOnSmallScreens = true,
            Gap = CssLength.Rem(2.5m),
            Padding = AllSpacing(CssLength.Rem(3)),
            Surface = Surface("#f8fafc", CssLength.Rem(1))
        });

        var introduction = Element("div");
        introduction.Children.Add(Element("h2", "Questions, answered", Typography(
            color: "#111827", size: CssLength.Rem(2.25m), weight: 700)));
        introduction.Children.Add(Element("p", "Use expandable answers when visitors need detail without a long wall of text.", Typography(
            color: "#4b5563", size: CssLength.Rem(1.05m))));

        var questions = Element("div", style: new HtmlStyle
        {
            Display = CssDisplay.Flex,
            FlexDirection = CssFlexDirection.Column,
            Gap = CssLength.Rem(0.75m)
        });
        questions.Children.Add(AccordionQuestion("What is included?", "Describe the product, service, or experience visitors receive."));
        questions.Children.Add(AccordionQuestion("How do I get started?", "Explain the simplest next step and what happens after it."));
        questions.Children.Add(AccordionQuestion("Where can I get help?", "Point visitors to the best support or contact option."));

        section.Children.Add(introduction);
        section.Children.Add(questions);
        return section;
    }

    /// <summary>Builds a responsive grid of value-and-label statistics.</summary>
    private HtmlNode CreateStatistics()
    {
        var section = Element("section", style: new HtmlStyle
        {
            Padding = AllSpacing(CssLength.Rem(3)),
            Surface = Surface("#111827")
        });
        section.Children.Add(Element("h2", "Results at a glance", Typography(
            color: "#ffffff", size: CssLength.Rem(2.25m), weight: 700, alignment: CssTextAlignment.Center)));

        var grid = Element("div", style: new HtmlStyle
        {
            Display = CssDisplay.Grid,
            GridColumns = 3,
            StackOnSmallScreens = true,
            Gap = CssLength.Rem(1.5m),
            Margin = Vertical(CssLength.Rem(2))
        });
        grid.Children.Add(Statistic("98", "98%", "Customer satisfaction"));
        grid.Children.Add(Statistic("24", "24 hours", "Average response time"));
        grid.Children.Add(Statistic("10", "10 years", "Serving our community"));
        section.Children.Add(grid);
        return section;
    }

    /// <summary>Builds a responsive media-and-copy section with safe placeholder media URLs.</summary>
    private HtmlNode CreateImageAndText()
    {
        var section = Element("section", style: new HtmlStyle
        {
            Display = CssDisplay.Flex,
            FlexDirection = CssFlexDirection.Row,
            StackOnSmallScreens = true,
            AlignItems = CssAlignment.Center,
            Gap = CssLength.Rem(2.5m),
            Padding = AllSpacing(CssLength.Rem(3))
        });
        var figure = Element("figure");
        figure.Children.Add(Image($"{PlaceholderBasePath}/hero.svg", "Describe the featured image"));
        figure.Children.Add(Element("figcaption", "Add an optional image caption."));

        var copy = Element("div", style: new HtmlStyle
        {
            Display = CssDisplay.Flex,
            FlexDirection = CssFlexDirection.Column,
            Gap = CssLength.Rem(1.25m)
        });
        copy.Children.Add(Element("h2", "Tell the story behind the image", Typography(
            color: "#111827", size: CssLength.Rem(2.25m), weight: 700)));
        copy.Children.Add(Element("p", "Pair a strong visual with concise copy that explains why it matters."));
        copy.Children.Add(Link("Learn more", "#", "#7c3aed", "#ffffff"));
        section.Children.Add(figure);
        section.Children.Add(copy);
        return section;
    }

    /// <summary>Builds a form skeleton only; submission behavior remains the host's responsibility.</summary>
    private HtmlNode CreateContactForm()
    {
        var section = Element("section", style: new HtmlStyle
        {
            Display = CssDisplay.Grid,
            GridColumns = 2,
            StackOnSmallScreens = true,
            Gap = CssLength.Rem(2.5m),
            Padding = AllSpacing(CssLength.Rem(3)),
            Surface = Surface("#f8fafc", CssLength.Rem(1))
        });
        var introduction = Element("div");
        introduction.Children.Add(Element("h2", "Let’s talk", Typography(
            color: "#111827", size: CssLength.Rem(2.25m), weight: 700)));
        introduction.Children.Add(Element("p", "Invite visitors to send a message. Connect this static form to processing later."));

        var form = Element("form", style: new HtmlStyle
        {
            Display = CssDisplay.Grid,
            GridColumns = 1,
            Gap = CssLength.Rem(1),
            Padding = AllSpacing(CssLength.Rem(1.5m)),
            Surface = Surface("#ffffff", CssLength.Rem(0.75m))
        });
        AddFormField(form, "Name", "text", "name", "Your name");
        AddFormField(form, "Email", "email", "email", "you@example.com");

        var message = catalog.CreateElement("textarea");
        var messageId = $"field-{message.NodeId}";
        message.Attributes["id"] = messageId;
        message.Attributes["name"] = "message";
        message.Attributes["rows"] = "5";
        message.Attributes["placeholder"] = "How can we help?";
        var messageLabel = Element("label", "Message");
        messageLabel.Attributes["for"] = messageId;
        var messageField = FormField();
        messageField.Children.Add(messageLabel);
        messageField.Children.Add(message);
        form.Children.Add(messageField);

        var submit = Element("button", "Send message", new HtmlStyle
        {
            Padding = AllSpacing(CssLength.Rem(0.75m)),
            Surface = Surface("#7c3aed", CssLength.Rem(0.5m)),
            Typography = new CssTypographyStyle
            {
                Color = CssColor.Hex("#ffffff"),
                FontWeight = 700,
                Alignment = CssTextAlignment.Center
            }
        });
        submit.Attributes["type"] = "submit";
        form.Children.Add(submit);
        section.Children.Add(introduction);
        section.Children.Add(form);
        return section;
    }

    /// <summary>Builds a figure-based image grid with editable alternative text.</summary>
    private HtmlNode CreateGallery()
    {
        var section = Element("section", style: new HtmlStyle
        {
            Padding = AllSpacing(CssLength.Rem(3))
        });
        section.Children.Add(Element("h2", "Gallery", Typography(
            color: "#111827", size: CssLength.Rem(2.25m), weight: 700, alignment: CssTextAlignment.Center)));
        var grid = Element("div", style: new HtmlStyle
        {
            Display = CssDisplay.Grid,
            GridColumns = 3,
            StackOnSmallScreens = true,
            Gap = CssLength.Rem(1),
            Margin = Vertical(CssLength.Rem(2))
        });
        grid.Children.Add(GalleryFigure($"{PlaceholderBasePath}/gallery-1.svg", "Gallery image one"));
        grid.Children.Add(GalleryFigure($"{PlaceholderBasePath}/gallery-2.svg", "Gallery image two"));
        grid.Children.Add(GalleryFigure($"{PlaceholderBasePath}/gallery-3.svg", "Gallery image three"));
        section.Children.Add(grid);
        return section;
    }

    /// <summary>Builds a semantic header and navigation tree without client-side behavior.</summary>
    private HtmlNode CreateNavigationHeader()
    {
        var header = Element("header", style: new HtmlStyle
        {
            Padding = AllSpacing(CssLength.Rem(1.25m)),
            Surface = Surface("#ffffff", CssLength.Rem(0.75m))
        });
        var navigation = Element("nav", style: new HtmlStyle
        {
            Display = CssDisplay.Flex,
            FlexDirection = CssFlexDirection.Row,
            StackOnSmallScreens = true,
            AlignItems = CssAlignment.Center,
            JustifyContent = CssJustification.SpaceBetween,
            Gap = CssLength.Rem(1)
        });
        navigation.Attributes["aria-label"] = "Main navigation";
        navigation.Children.Add(Link("Your brand", "#", "#111827", "#ffffff"));

        var links = Element("div", style: new HtmlStyle
        {
            Display = CssDisplay.Flex,
            FlexDirection = CssFlexDirection.Row,
            StackOnSmallScreens = true,
            Gap = CssLength.Rem(0.75m)
        });
        links.Children.Add(Link("About", "#", "#ffffff", "#374151"));
        links.Children.Add(Link("Services", "#", "#ffffff", "#374151"));
        links.Children.Add(Link("Contact", "#", "#7c3aed", "#ffffff"));
        navigation.Children.Add(links);
        header.Children.Add(navigation);
        return header;
    }

    /// <summary>Builds an accessible collection of placeholder organization marks.</summary>
    private HtmlNode CreateLogoCloud()
    {
        var section = Element("section", style: new HtmlStyle
        {
            Padding = AllSpacing(CssLength.Rem(3)),
            Surface = Surface("#f8fafc", CssLength.Rem(1))
        });
        section.Children.Add(Element("h2", "Trusted by teams like yours", Typography(
            color: "#111827", size: CssLength.Rem(1.75m), weight: 700, alignment: CssTextAlignment.Center)));
        section.Children.Add(Element("p", "Replace these editable partner names with organizations you work with.", Typography(
            color: "#4b5563", alignment: CssTextAlignment.Center)));

        var logos = Element("ul", style: new HtmlStyle
        {
            Display = CssDisplay.Grid,
            GridColumns = 3,
            StackOnSmallScreens = true,
            Gap = CssLength.Rem(1),
            Margin = Vertical(CssLength.Rem(2))
        });
        logos.Children.Add(Logo("Northstar"));
        logos.Children.Add(Logo("Juniper"));
        logos.Children.Add(Logo("Summit"));
        logos.Children.Add(Logo("Harbor"));
        logos.Children.Add(Logo("Lumen"));
        logos.Children.Add(Logo("Cedar"));
        section.Children.Add(logos);
        return section;
    }

    /// <summary>Builds a responsive set of pricing cards with ordinary links and lists.</summary>
    private HtmlNode CreatePricingGrid()
    {
        var section = Element("section", style: new HtmlStyle
        {
            Padding = AllSpacing(CssLength.Rem(3))
        });
        section.Children.Add(Element("h2", "Plans for every stage", Typography(
            color: "#111827", size: CssLength.Rem(2.25m), weight: 700, alignment: CssTextAlignment.Center)));
        section.Children.Add(Element("p", "Present a clear choice with editable pricing, benefits, and calls to action.", Typography(
            color: "#4b5563", alignment: CssTextAlignment.Center)));

        var grid = Element("div", style: new HtmlStyle
        {
            Display = CssDisplay.Grid,
            GridColumns = 3,
            StackOnSmallScreens = true,
            Gap = CssLength.Rem(1.5m),
            Margin = Vertical(CssLength.Rem(2))
        });
        grid.Children.Add(PricingCard("Starter", "$19", "For getting started", ["One project", "Email support", "Core reporting"], "Choose Starter", "#ffffff", "#111827"));
        grid.Children.Add(PricingCard("Growth", "$49", "For growing teams", ["Five projects", "Priority support", "Advanced reporting"], "Choose Growth", "#7c3aed", "#ffffff"));
        grid.Children.Add(PricingCard("Scale", "$99", "For established organizations", ["Unlimited projects", "Dedicated support", "Custom reporting"], "Choose Scale", "#ffffff", "#111827"));
        section.Children.Add(grid);
        return section;
    }

    /// <summary>Builds a responsive set of team profiles with placeholder images.</summary>
    private HtmlNode CreateTeamGrid()
    {
        var section = Element("section", style: new HtmlStyle
        {
            Padding = AllSpacing(CssLength.Rem(3)),
            Surface = Surface("#f8fafc", CssLength.Rem(1))
        });
        section.Children.Add(Element("h2", "Meet the team", Typography(
            color: "#111827", size: CssLength.Rem(2.25m), weight: 700, alignment: CssTextAlignment.Center)));
        section.Children.Add(Element("p", "Introduce the people behind your organization with editable images and descriptions.", Typography(
            color: "#4b5563", alignment: CssTextAlignment.Center)));

        var grid = Element("div", style: new HtmlStyle
        {
            Display = CssDisplay.Grid,
            GridColumns = 3,
            StackOnSmallScreens = true,
            Gap = CssLength.Rem(1.5m),
            Margin = Vertical(CssLength.Rem(2))
        });
        grid.Children.Add(TeamMember("Alex Morgan", "Founder", "gallery-1.svg"));
        grid.Children.Add(TeamMember("Jordan Lee", "Creative director", "gallery-2.svg"));
        grid.Children.Add(TeamMember("Taylor Chen", "Customer success", "gallery-3.svg"));
        section.Children.Add(grid);
        return section;
    }

    /// <summary>Builds a semantic footer with grouped navigation links.</summary>
    private HtmlNode CreateSiteFooter()
    {
        var section = Element("section", style: new HtmlStyle
        {
            Padding = AllSpacing(CssLength.Rem(3)),
            Surface = Surface("#111827", CssLength.Rem(1))
        });
        section.Children.Add(Element("h2", "Explore", Typography(
            color: "#ffffff", size: CssLength.Rem(1.75m), weight: 700)));

        var grid = Element("div", style: new HtmlStyle
        {
            Display = CssDisplay.Grid,
            GridColumns = 3,
            StackOnSmallScreens = true,
            Gap = CssLength.Rem(1.5m),
            Margin = Vertical(CssLength.Rem(1.5m))
        });
        grid.Children.Add(FooterLinkGroup("Company", ["About us", "Our team", "Careers"]));
        grid.Children.Add(FooterLinkGroup("Resources", ["Guides", "Support", "Contact"]));
        grid.Children.Add(FooterLinkGroup("Connect", ["Newsletter", "Community", "Updates"]));
        section.Children.Add(grid);
        return section;
    }

    /// <summary>Builds a subscription form skeleton without assigning transport or persistence behavior.</summary>
    private HtmlNode CreateNewsletterSignup()
    {
        var section = Element("section", style: new HtmlStyle
        {
            Padding = AllSpacing(CssLength.Rem(2.5m)),
            Surface = Surface("#f8fafc", CssLength.Rem(1))
        });
        section.Children.Add(Element("h2", "Stay in the loop", Typography(
            color: "#111827", size: CssLength.Rem(2), weight: 700)));
        section.Children.Add(Element("p", "Share updates, ideas, and useful resources with your subscribers.", Typography(
            color: "#4b5563")));

        var form = Element("form", style: new HtmlStyle
        {
            Display = CssDisplay.Flex,
            FlexDirection = CssFlexDirection.Row,
            StackOnSmallScreens = true,
            AlignItems = CssAlignment.Center,
            Gap = CssLength.Rem(0.75m),
            Margin = Vertical(CssLength.Rem(1.25m))
        });
        var input = catalog.CreateElement("input");
        var inputId = $"newsletter-{input.NodeId}";
        input.Attributes["id"] = inputId;
        input.Attributes["type"] = "email";
        input.Attributes["name"] = "email";
        input.Attributes["placeholder"] = "you@example.com";
        var label = Element("label", "Email address");
        label.Attributes["for"] = inputId;
        var button = Element("button", "Subscribe", new HtmlStyle
        {
            Padding = new CssLogicalSpacing
            {
                BlockStart = CssLength.Rem(0.75m),
                InlineEnd = CssLength.Rem(1.25m),
                BlockEnd = CssLength.Rem(0.75m),
                InlineStart = CssLength.Rem(1.25m)
            },
            Surface = Surface("#7c3aed", CssLength.Rem(0.5m)),
            Typography = new CssTypographyStyle
            {
                Color = CssColor.Hex("#ffffff"),
                FontWeight = 600,
                Alignment = CssTextAlignment.Center
            }
        });
        button.Attributes["type"] = "button";
        form.Children.Add(label);
        form.Children.Add(input);
        form.Children.Add(button);
        section.Children.Add(form);
        return section;
    }

    /// <summary>Builds an announcement region and a non-functional author-editable dismiss button.</summary>
    private HtmlNode CreateAnnouncementBanner()
    {
        var banner = Element("aside", style: new HtmlStyle
        {
            Display = CssDisplay.Flex,
            FlexDirection = CssFlexDirection.Row,
            StackOnSmallScreens = true,
            AlignItems = CssAlignment.Center,
            JustifyContent = CssJustification.SpaceBetween,
            Gap = CssLength.Rem(1),
            Padding = AllSpacing(CssLength.Rem(1.25m))
        });
        var copy = Element("div");
        copy.Children.Add(Element("h2", "New resources are available", Typography(
            color: "#4c1d95", size: CssLength.Rem(1.25m), weight: 700)));
        copy.Children.Add(Element("p", "Share a timely update without adding scripts or dismissal behavior.", Typography(
            color: "#5b21b6")));
        banner.Children.Add(copy);
        banner.Children.Add(Link("Read the update", "#", "#7c3aed", "#ffffff"));
        return banner;
    }

    /// <summary>Builds placeholder article summaries; content-query integration remains outside the HTML model.</summary>
    private HtmlNode CreateLatestArticles()
    {
        var section = Element("section", style: new HtmlStyle
        {
            Padding = AllSpacing(CssLength.Rem(3))
        });
        section.Children.Add(Element("h2", "Latest articles", Typography(
            color: "#111827", size: CssLength.Rem(2.25m), weight: 700, alignment: CssTextAlignment.Center)));
        section.Children.Add(Element("p", "Highlight a few useful reads with fully editable static placeholders.", Typography(
            color: "#4b5563", alignment: CssTextAlignment.Center)));

        var grid = Element("div", style: new HtmlStyle
        {
            Display = CssDisplay.Grid,
            GridColumns = 3,
            StackOnSmallScreens = true,
            Gap = CssLength.Rem(1.5m),
            Margin = Vertical(CssLength.Rem(2))
        });
        grid.Children.Add(ArticleCard("Make your next page clearer", "A short introduction to communicating the value visitors need to see first."));
        grid.Children.Add(ArticleCard("Build a helpful content rhythm", "Use simple, focused articles to keep your audience informed over time."));
        grid.Children.Add(ArticleCard("Turn interest into a next step", "Give each reader a clear, relevant action after they finish reading."));
        section.Children.Add(grid);
        return section;
    }

    /// <summary>Builds an ordered set of process stages with visible sequence labels.</summary>
    private HtmlNode CreateProcessSteps()
    {
        var section = Element("section", style: new HtmlStyle
        {
            Padding = AllSpacing(CssLength.Rem(3)),
            Surface = Surface("#f8fafc", CssLength.Rem(1))
        });
        var header = Element("header");
        header.Children.Add(Element("h2", "A simple process", Typography(
            color: "#111827", size: CssLength.Rem(2.25m), weight: 700, alignment: CssTextAlignment.Center)));
        header.Children.Add(Element("p", "Use three clear steps to explain how visitors can get started.", Typography(
            color: "#4b5563", alignment: CssTextAlignment.Center)));

        var steps = Element("ol");
        steps.Children.Add(ProcessStep("01", "Start with your goal", "Describe the first simple action a visitor should take."));
        steps.Children.Add(ProcessStep("02", "Choose your approach", "Help people understand the path that best fits their needs."));
        steps.Children.Add(ProcessStep("03", "Move forward with confidence", "Finish with a useful outcome and a clear next step."));
        section.Children.Add(header);
        section.Children.Add(steps);
        return section;
    }

    /// <summary>Builds a media-led showcase collection from ordinary article nodes.</summary>
    private HtmlNode CreateShowcaseCollection()
    {
        var section = Element("section", style: new HtmlStyle
        {
            Padding = AllSpacing(CssLength.Rem(3))
        });
        var header = Element("header");
        header.Children.Add(Element("h2", "Explore the collection", Typography(
            color: "#111827", size: CssLength.Rem(2.25m), weight: 700, alignment: CssTextAlignment.Center)));
        header.Children.Add(Element("p", "Highlight a small collection of static, editable offerings or resources.", Typography(
            color: "#4b5563", alignment: CssTextAlignment.Center)));

        var collection = Element("ul", style: new HtmlStyle
        {
            Display = CssDisplay.Grid,
            GridColumns = 3,
            StackOnSmallScreens = true,
            Gap = CssLength.Rem(1.5m),
            Margin = Vertical(CssLength.Rem(2))
        });
        collection.Children.Add(ShowcaseItem("Collection one", "A concise introduction to the first item in this editable collection.", "gallery-1.svg"));
        collection.Children.Add(ShowcaseItem("Collection two", "A concise introduction to the second item in this editable collection.", "gallery-2.svg"));
        collection.Children.Add(ShowcaseItem("Collection three", "A concise introduction to the third item in this editable collection.", "gallery-3.svg"));
        section.Children.Add(header);
        section.Children.Add(collection);
        return section;
    }

    /// <summary>Builds a chronological milestone list using machine-readable time elements.</summary>
    private HtmlNode CreateMilestoneTimeline()
    {
        var section = Element("section", style: new HtmlStyle
        {
            Padding = AllSpacing(CssLength.Rem(3)),
            Surface = Surface("#f8fafc", CssLength.Rem(1))
        });
        var header = Element("header");
        header.Children.Add(Element("h2", "A timeline worth sharing", Typography(
            color: "#111827", size: CssLength.Rem(2.25m), weight: 700, alignment: CssTextAlignment.Center)));
        header.Children.Add(Element("p", "Show the key moments, launches, or milestones behind your work.", Typography(
            color: "#4b5563", alignment: CssTextAlignment.Center)));

        var milestones = Element("ol", style: new HtmlStyle { Margin = Vertical(CssLength.Rem(2)) });
        milestones.Children.Add(Milestone("2026-01-01", "January 2026", "The idea takes shape", "Introduce the first moment that set this work in motion."));
        milestones.Children.Add(Milestone("2026-04-01", "April 2026", "A clearer direction", "Describe the decision or release that moved the work forward."));
        milestones.Children.Add(Milestone("2026-07-01", "July 2026", "Ready to share", "Finish with the milestone visitors should remember today."));
        section.Children.Add(header);
        section.Children.Add(milestones);
        return section;
    }

    /// <summary>Builds an accessible comparison table with scoped column and row headers.</summary>
    private HtmlNode CreateFeatureComparisonTable()
    {
        var section = Element("section", style: new HtmlStyle { Padding = AllSpacing(CssLength.Rem(3)) });
        var header = Element("header");
        header.Children.Add(Element("h2", "Compare what matters", Typography(
            color: "#111827", size: CssLength.Rem(2.25m), weight: 700, alignment: CssTextAlignment.Center)));
        header.Children.Add(Element("p", "Use a compact comparison to make an editable choice easier to understand.", Typography(
            color: "#4b5563", alignment: CssTextAlignment.Center)));

        var table = Element("table", style: new HtmlStyle
        {
            Margin = Vertical(CssLength.Rem(2)),
            Surface = Surface("#ffffff", CssLength.Rem(0.75m))
        });
        table.Children.Add(Element("caption", "Feature comparison", Typography(
            color: "#374151", size: CssLength.Rem(1.05m), weight: 700)));

        var head = Element("thead", style: new HtmlStyle { Surface = Surface("#f3f4f6") });
        var headerRow = Element("tr");
        headerRow.Children.Add(TableHeader("Feature", "col"));
        headerRow.Children.Add(TableHeader("Basic", "col"));
        headerRow.Children.Add(TableHeader("Pro", "col"));
        head.Children.Add(headerRow);

        var body = Element("tbody");
        body.Children.Add(ComparisonRow("Workspace", "Yes", "Yes"));
        body.Children.Add(ComparisonRow("Members", "5", "Any"));
        body.Children.Add(ComparisonRow("Reports", "Add-on", "Yes"));
        body.Children.Add(ComparisonRow("Support", "—", "Yes"));
        table.Children.Add(head);
        table.Children.Add(body);
        section.Children.Add(header);
        section.Children.Add(table);
        return section;
    }

    /// <summary>Builds a no-script supplementary-details list from native disclosure elements.</summary>
    private HtmlNode CreateDetailsList()
    {
        var section = Element("section", style: new HtmlStyle
        {
            Padding = AllSpacing(CssLength.Rem(3)),
            Surface = Surface("#f8fafc", CssLength.Rem(1))
        });
        var header = Element("header");
        header.Children.Add(Element("h2", "The details at a glance", Typography(
            color: "#111827", size: CssLength.Rem(2.25m), weight: 700, alignment: CssTextAlignment.Center)));
        header.Children.Add(Element("p", "Present specifications, service facts, or practical information in an easy-to-edit structure.", Typography(
            color: "#4b5563", alignment: CssTextAlignment.Center)));

        var details = Element("dl", style: new HtmlStyle
        {
            Display = CssDisplay.Grid,
            GridColumns = 2,
            StackOnSmallScreens = true,
            Gap = CssLength.Rem(1),
            Margin = Vertical(CssLength.Rem(2))
        });
        AddDetail(details, "Typical response", "Within one business day");
        AddDetail(details, "Availability", "Monday through Friday");
        AddDetail(details, "Delivery", "Remote or on site");
        AddDetail(details, "Getting started", "A short discovery conversation");
        section.Children.Add(header);
        section.Children.Add(details);
        return section;
    }

    /// <summary>Builds dialog markup only; opening, focus management, and actions remain host responsibilities.</summary>
    private HtmlNode CreateConfirmationDialog()
    {
        var section = Element("section", style: new HtmlStyle
        {
            Padding = AllSpacing(CssLength.Rem(3)),
            Surface = Surface("#f8fafc", CssLength.Rem(1))
        });
        var introduction = Element("header");
        introduction.Children.Add(Element("h2", "Confirmation dialog", Typography(
            color: "#111827", size: CssLength.Rem(2.25m), weight: 700, alignment: CssTextAlignment.Center)));
        introduction.Children.Add(Element("p", "Edit the message and actions, then choose whether the dialog begins open.", Typography(
            color: "#4b5563", alignment: CssTextAlignment.Center)));

        var dialog = Element("dialog", style: new HtmlStyle
        {
            Display = CssDisplay.Flex,
            FlexDirection = CssFlexDirection.Column,
            Gap = CssLength.Rem(1),
            Margin = Vertical(CssLength.Rem(2)),
            Padding = AllSpacing(CssLength.Rem(1.5m)),
            Surface = Surface("#ffffff", CssLength.Rem(0.75m))
        });
        dialog.Attributes["open"] = string.Empty;
        dialog.Attributes["aria-labelledby"] = "confirmation-dialog-title";

        var title = Element("h3", "Confirm this action", Typography(
            color: "#111827", size: CssLength.Rem(1.5m), weight: 700));
        title.Attributes["id"] = "confirmation-dialog-title";
        dialog.Children.Add(title);
        dialog.Children.Add(Element(
            "p",
            "Review the details before continuing. You can change this message for the action your page requires.",
            Typography(color: "#4b5563")));

        var actions = Element("div", style: new HtmlStyle
        {
            Display = CssDisplay.Flex,
            Gap = CssLength.Rem(0.75m)
        });
        var cancel = DialogButton("Cancel", "#e5e7eb", "#111827");
        var confirm = DialogButton("Continue", "#7c3aed", "#ffffff");
        actions.Children.Add(cancel);
        actions.Children.Add(confirm);
        dialog.Children.Add(actions);

        section.Children.Add(introduction);
        section.Children.Add(dialog);
        return section;
    }

    /// <summary>Creates a styled button node for a dialog action without attaching behavior.</summary>
    private HtmlNode DialogButton(string text, string backgroundColor, string textColor)
    {
        var button = Element("button", text, new HtmlStyle
        {
            Padding = new CssLogicalSpacing
            {
                BlockStart = CssLength.Rem(0.75m),
                InlineEnd = CssLength.Rem(1.25m),
                BlockEnd = CssLength.Rem(0.75m),
                InlineStart = CssLength.Rem(1.25m)
            },
            Surface = Surface(backgroundColor, CssLength.Rem(0.5m)),
            Typography = new CssTypographyStyle
            {
                Color = CssColor.Hex(textColor),
                FontWeight = 600,
                Alignment = CssTextAlignment.Center
            }
        });
        button.Attributes["type"] = "button";
        return button;
    }

    /// <summary>Creates one process-stage item with an author-visible sequence marker.</summary>
    private HtmlNode ProcessStep(string number, string heading, string body)
    {
        var item = Element("li");
        var card = Element("div", style: new HtmlStyle
        {
            Display = CssDisplay.Flex,
            FlexDirection = CssFlexDirection.Column,
            Gap = CssLength.Rem(0.75m),
            Padding = AllSpacing(CssLength.Rem(1.5m)),
            Surface = Surface("#ffffff", CssLength.Rem(0.75m))
        });
        card.Children.Add(Element("span", number, Typography(
            color: "#7c3aed", size: CssLength.Rem(1.125m), weight: 800)));
        card.Children.Add(Element("h3", heading, Typography(
            color: "#111827", size: CssLength.Rem(1.25m), weight: 700)));
        card.Children.Add(Element("p", body, Typography(color: "#4b5563")));
        item.Children.Add(card);
        return item;
    }

    /// <summary>Creates one milestone with separate machine-readable and visible dates.</summary>
    private HtmlNode Milestone(string dateTime, string visibleDate, string heading, string body)
    {
        var item = Element("li");
        var article = Element("article", style: new HtmlStyle
        {
            Display = CssDisplay.Flex,
            FlexDirection = CssFlexDirection.Column,
            Gap = CssLength.Rem(0.5m),
            Padding = AllSpacing(CssLength.Rem(1.25m)),
            Surface = Surface("#ffffff", CssLength.Rem(0.75m))
        });
        var time = Element("time", visibleDate, Typography(color: "#7c3aed", weight: 700));
        time.Attributes["datetime"] = dateTime;
        article.Children.Add(time);
        article.Children.Add(Element("h3", heading, Typography(color: "#111827", size: CssLength.Rem(1.25m), weight: 700)));
        article.Children.Add(Element("p", body, Typography(color: "#4b5563")));
        item.Children.Add(article);
        return item;
    }

    /// <summary>Creates a table header whose supplied scope preserves row or column semantics.</summary>
    private HtmlNode TableHeader(string text, string scope)
    {
        var cell = Element("th", text, new HtmlStyle
        {
            Padding = AllSpacing(CssLength.Rem(0.4m)),
            Typography = new CssTypographyStyle { Color = CssColor.Hex("#111827"), FontWeight = 700 }
        });
        cell.Attributes["scope"] = scope;
        return cell;
    }

    /// <summary>Creates one feature row with a row header and two comparison cells.</summary>
    private HtmlNode ComparisonRow(string feature, string standard, string premium)
    {
        var row = Element("tr");
        row.Children.Add(TableHeader(feature, "row"));
        row.Children.Add(Element("td", standard, new HtmlStyle { Padding = AllSpacing(CssLength.Rem(0.4m)) }));
        row.Children.Add(Element("td", premium, new HtmlStyle { Padding = AllSpacing(CssLength.Rem(0.4m)) }));
        return row;
    }

    /// <summary>Appends one term-description pair to an existing description list.</summary>
    private void AddDetail(HtmlNode list, string term, string description)
    {
        list.Children.Add(Element("dt", term, Typography(color: "#111827", weight: 700)));
        list.Children.Add(Element("dd", description, new HtmlStyle
        {
            Padding = AllSpacing(CssLength.Rem(0.75m)),
            Typography = new CssTypographyStyle { Color = CssColor.Hex("#4b5563") }
        }));
    }

    /// <summary>Creates one showcase article using a local placeholder image asset.</summary>
    private HtmlNode ShowcaseItem(string heading, string summary, string imageName)
    {
        var item = Element("li");
        var article = Element("article", style: new HtmlStyle
        {
            Display = CssDisplay.Flex,
            FlexDirection = CssFlexDirection.Column,
            Gap = CssLength.Rem(0.75m),
            Padding = AllSpacing(CssLength.Rem(1.25m)),
            Surface = Surface("#f8fafc", CssLength.Rem(0.75m))
        });
        var figure = Element("figure");
        var imageLink = Element("a");
        imageLink.Attributes["href"] = "#";
        imageLink.Children.Add(Image($"{PlaceholderBasePath}/{imageName}", $"Placeholder image for {heading}"));
        figure.Children.Add(imageLink);
        var headingElement = Element("h3", style: Typography(
            color: "#111827", size: CssLength.Rem(1.25m), weight: 700));
        var headingLink = Element("a", heading);
        headingLink.Attributes["href"] = "#";
        headingElement.Children.Add(headingLink);
        article.Children.Add(figure);
        article.Children.Add(headingElement);
        article.Children.Add(Element("p", summary, Typography(color: "#4b5563")));
        article.Children.Add(Link("Explore collection", "#", "#ffffff", "#4c1d95"));
        item.Children.Add(article);
        return item;
    }

    /// <summary>Creates one footer navigation group while preserving the caller's link order.</summary>
    private HtmlNode FooterLinkGroup(string heading, IReadOnlyList<string> links)
    {
        var group = Element("div");
        group.Children.Add(Element("h3", heading, Typography(
            color: "#ffffff", size: CssLength.Rem(1.125m), weight: 700)));
        var list = Element("ul");
        foreach (var linkText in links)
        {
            var item = Element("li");
            var link = Element("a", linkText, Typography(color: "#d1d5db"));
            link.Attributes["href"] = "#";
            item.Children.Add(link);
            list.Children.Add(item);
        }

        group.Children.Add(list);
        return group;
    }

    /// <summary>Creates one static article summary card without coupling it to a content query.</summary>
    private HtmlNode ArticleCard(string heading, string summary)
    {
        var article = Element("article", style: new HtmlStyle
        {
            Display = CssDisplay.Flex,
            FlexDirection = CssFlexDirection.Column,
            Gap = CssLength.Rem(0.75m),
            Padding = AllSpacing(CssLength.Rem(1.5m)),
            Surface = Surface("#f8fafc", CssLength.Rem(0.75m))
        });
        article.Children.Add(Element("h3", heading, Typography(color: "#111827", size: CssLength.Rem(1.25m), weight: 700)));
        article.Children.Add(Element("p", summary, Typography(color: "#4b5563")));
        article.Children.Add(Link("Read article", "#", "#ffffff", "#4c1d95"));
        return article;
    }

    /// <summary>Creates a placeholder organization mark with accessible alternative text.</summary>
    private HtmlNode Logo(string name)
    {
        var item = Element("li");
        var link = Element("a");
        link.Attributes["href"] = "#";
        link.Children.Add(Element("span", name));
        item.Children.Add(link);
        return item;
    }

    /// <summary>Creates one pricing option with ordered benefits and an author-editable action link.</summary>
    private HtmlNode PricingCard(
        string name,
        string price,
        string description,
        IReadOnlyList<string> benefits,
        string action,
        string actionBackground,
        string actionText)
    {
        var article = Element("article", style: new HtmlStyle
        {
            Display = CssDisplay.Flex,
            FlexDirection = CssFlexDirection.Column,
            Gap = CssLength.Rem(1),
            Padding = AllSpacing(CssLength.Rem(1.5m)),
            Surface = Surface("#ffffff", CssLength.Rem(0.75m))
        });
        article.Children.Add(Element("h3", name, Typography(color: "#111827", size: CssLength.Rem(1.5m), weight: 700)));
        article.Children.Add(Element("p", price, Typography(color: "#7c3aed", size: CssLength.Rem(2.25m), weight: 800)));
        article.Children.Add(Element("p", description, Typography(color: "#4b5563")));
        var list = Element("ul");
        foreach (var benefit in benefits)
        {
            list.Children.Add(Element("li", benefit));
        }

        article.Children.Add(list);
        article.Children.Add(Link(action, "#", actionBackground, actionText));
        return article;
    }

    /// <summary>Creates one team profile using a local placeholder image asset.</summary>
    private HtmlNode TeamMember(string name, string role, string imageName)
    {
        var article = Element("article", style: new HtmlStyle
        {
            Padding = AllSpacing(CssLength.Rem(1.25m)),
            Surface = Surface("#ffffff", CssLength.Rem(0.75m))
        });
        var figure = Element("figure");
        figure.Children.Add(Image($"{PlaceholderBasePath}/{imageName}", $"Portrait placeholder for {name}"));
        figure.Children.Add(Element("figcaption", $"Replace this portrait of {name} with your own image."));
        article.Children.Add(figure);
        article.Children.Add(Element("h3", name, Typography(color: "#111827", size: CssLength.Rem(1.25m), weight: 700)));
        article.Children.Add(Element("p", role, Typography(color: "#4b5563")));
        return article;
    }

    /// <summary>Creates one statistic with separate machine-readable and display values.</summary>
    private HtmlNode Statistic(string value, string display, string label)
    {
        var article = Element("article", style: new HtmlStyle
        {
            Padding = AllSpacing(CssLength.Rem(1.5m)),
            Surface = Surface("#1f2937", CssLength.Rem(0.75m))
        });
        var data = Element("data", display, Typography(
            color: "#c4b5fd", size: CssLength.Rem(2.5m), weight: 800, alignment: CssTextAlignment.Center));
        data.Attributes["value"] = value;
        article.Children.Add(data);
        article.Children.Add(Element("p", label, Typography(
            color: "#e5e7eb", alignment: CssTextAlignment.Center)));
        return article;
    }

    // Daisy templates deliberately use literal prefixed tokens so Tailwind source scanning retains them.
    private HtmlNode CreateDaisyButton()
    {
        var button = Element("button", "Get started");
        button.Attributes["type"] = "button";
        button.ThemeClasses.AddRange(["d-btn", "d-btn-primary"]);
        return button;
    }

    private HtmlNode CreateDaisyBadge()
    {
        var badge = Element("span", "New");
        badge.ThemeClasses.AddRange(["d-badge", "d-badge-primary"]);
        return badge;
    }

    private HtmlNode CreateDaisyAlert()
    {
        var alert = Element("div");
        alert.Attributes["role"] = "status";
        alert.ThemeClasses.AddRange(["d-alert", "d-alert-info"]);
        alert.Children.Add(Element("span", "This is a helpful status message."));
        return alert;
    }

    private HtmlNode CreateDaisyCard()
    {
        var card = Element("article");
        card.ThemeClasses.AddRange(["d-card", "d-card-border", "bg-base-100"]);
        var body = Element("div");
        body.ThemeClasses.Add("d-card-body");
        body.Children.Add(Element("h2", "Card title"));
        body.Children.Add(Element("p", "Use this card to present a focused piece of content."));
        var actions = Element("div");
        actions.ThemeClasses.AddRange(["d-card-actions", "justify-end"]);
        var action = CreateDaisyButton();
        action.Children.Clear();
        action.Children.Add(HtmlNode.CreateText("Learn more"));
        actions.Children.Add(action);
        body.Children.Add(actions);
        card.Children.Add(body);
        return card;
    }

    private HtmlNode CreateDaisyHero()
    {
        var hero = Element("section");
        hero.ThemeClasses.AddRange(["d-hero", "bg-base-200"]);
        var content = Element("div");
        content.ThemeClasses.Add("d-hero-content");
        var copy = Element("div");
        copy.Children.Add(Element("h1", "Build something remarkable"));
        copy.Children.Add(Element("p", "Start with a clear message and a useful next step."));
        copy.Children.Add(CreateDaisyButton());
        content.Children.Add(copy);
        hero.Children.Add(content);
        return hero;
    }

    private HtmlNode CreateDaisyStat()
    {
        var stats = Element("section");
        stats.ThemeClasses.Add("d-stats");
        var stat = Element("div");
        stat.ThemeClasses.Add("d-stat");
        var title = Element("div", "Total visitors");
        title.ThemeClasses.Add("d-stat-title");
        var value = Element("div", "31,000");
        value.ThemeClasses.Add("d-stat-value");
        var description = Element("div", "21% more than last month");
        description.ThemeClasses.Add("d-stat-desc");
        stat.Children.Add(title);
        stat.Children.Add(value);
        stat.Children.Add(description);
        stats.Children.Add(stat);
        return stats;
    }

    private HtmlNode CreateDaisyProgress()
    {
        var progress = Element("progress");
        progress.Attributes["value"] = "70";
        progress.Attributes["max"] = "100";
        progress.Attributes["aria-label"] = "Profile completion: 70 percent";
        progress.ThemeClasses.AddRange(["d-progress", "d-progress-primary"]);
        return progress;
    }

    private HtmlNode CreateDaisySkeleton()
    {
        var skeleton = Element("div");
        skeleton.Attributes["aria-label"] = "Loading content";
        skeleton.Attributes["role"] = "status";
        skeleton.ThemeClasses.AddRange(["d-skeleton", "h-24", "w-full"]);
        return skeleton;
    }

    private HtmlNode CreateDaisyDivider()
    {
        var divider = Element("div", "More information");
        divider.Attributes["role"] = "separator";
        divider.ThemeClasses.Add("d-divider");
        return divider;
    }

    private HtmlNode CreateDaisyBreadcrumbs()
    {
        var nav = Element("nav");
        nav.Attributes["aria-label"] = "Breadcrumb";
        nav.ThemeClasses.Add("d-breadcrumbs");
        var list = Element("ul");
        foreach (var label in new[] { "Home", "Products", "Current page" })
        {
            var item = Element("li");
            var link = Element("a", label);
            link.Attributes["href"] = "#";
            item.Children.Add(link);
            list.Children.Add(item);
        }
        nav.Children.Add(list);
        return nav;
    }

    private HtmlNode CreateDaisySteps()
    {
        var steps = Element("ul");
        steps.ThemeClasses.AddRange(["d-steps", "d-steps-vertical"]);
        foreach (var label in new[] { "Plan", "Build", "Publish" })
        {
            var item = Element("li", label);
            item.ThemeClasses.Add("d-step");
            steps.Children.Add(item);
        }
        return steps;
    }

    private HtmlNode CreateDaisyTimeline()
    {
        var timeline = Element("ul");
        timeline.ThemeClasses.AddRange(["d-timeline", "d-timeline-vertical"]);
        foreach (var (date, milestone) in new[]
                 {
                     ("2024", "Project started"),
                     ("2025", "First release"),
                     ("Today", "Keep improving")
                 })
        {
            var item = Element("li");
            var start = Element("div", date);
            start.ThemeClasses.Add("d-timeline-start");
            var middle = Element("div", "•");
            middle.Attributes["aria-hidden"] = "true";
            middle.ThemeClasses.Add("d-timeline-middle");
            var content = Element("div", milestone);
            content.ThemeClasses.AddRange(["d-timeline-end", "d-timeline-box"]);
            item.Children.Add(start);
            item.Children.Add(middle);
            item.Children.Add(content);
            item.Children.Add(Element("hr"));
            timeline.Children.Add(item);
        }
        return timeline;
    }

    private HtmlNode CreateDaisyTable()
    {
        var table = Element("table");
        table.ThemeClasses.Add("d-table");
        var header = Element("thead");
        var headerRow = Element("tr");
        headerRow.Children.Add(Element("th", "Feature"));
        headerRow.Children.Add(Element("th", "Included"));
        header.Children.Add(headerRow);
        var body = Element("tbody");
        var row = Element("tr");
        row.Children.Add(Element("td", "Editor access"));
        row.Children.Add(Element("td", "Yes"));
        body.Children.Add(row);
        table.Children.Add(header);
        table.Children.Add(body);
        return table;
    }

    private HtmlNode CreateDaisyPagination()
    {
        var nav = Element("nav");
        nav.Attributes["aria-label"] = "Pagination";
        var list = Element("div");
        list.ThemeClasses.Add("d-join");
        foreach (var label in new[] { "Previous", "1", "2", "Next" })
        {
            var button = Element("button", label);
            button.Attributes["type"] = "button";
            button.ThemeClasses.AddRange(["d-join-item", "d-btn"]);
            list.Children.Add(button);
        }
        nav.Children.Add(list);
        return nav;
    }

    private HtmlNode CreateDaisyAccordion()
    {
        var details = Element("details");
        details.ThemeClasses.AddRange(["d-collapse", "d-collapse-arrow", "bg-base-200"]);
        var summary = Element("summary", "What is included?");
        summary.ThemeClasses.Add("d-collapse-title");
        var content = Element("div", "Everything in this component is ordinary editable HTML.");
        content.ThemeClasses.Add("d-collapse-content");
        details.Children.Add(summary);
        details.Children.Add(content);
        return details;
    }

    /// <summary>Appends a labeled input while keeping label and control association explicit.</summary>
    private void AddFormField(HtmlNode form, string labelText, string type, string name, string placeholder)
    {
        var input = catalog.CreateElement("input");
        var inputId = $"field-{input.NodeId}";
        input.Attributes["id"] = inputId;
        input.Attributes["type"] = type;
        input.Attributes["name"] = name;
        input.Attributes["placeholder"] = placeholder;
        var label = Element("label", labelText);
        label.Attributes["for"] = inputId;
        var field = FormField();
        field.Children.Add(label);
        field.Children.Add(input);
        form.Children.Add(field);
    }

    /// <summary>Creates the shared visual container for a label-control pair.</summary>
    private HtmlNode FormField() => Element("div", style: new HtmlStyle
    {
        Display = CssDisplay.Flex,
        FlexDirection = CssFlexDirection.Column,
        Gap = CssLength.Rem(0.5m)
    });

    /// <summary>Creates a gallery figure around an image with author-supplied alternative text.</summary>
    private HtmlNode GalleryFigure(string source, string alternativeText)
    {
        var figure = Element("figure");
        figure.Children.Add(Image(source, alternativeText));
        figure.Children.Add(Element("figcaption", alternativeText));
        return figure;
    }

    /// <summary>Creates an image node with explicit source, alternative text, and lazy-loading intent.</summary>
    private HtmlNode Image(string source, string alternativeText)
    {
        var image = catalog.CreateElement("img");
        image.Attributes["src"] = source;
        image.Attributes["alt"] = alternativeText;
        image.Attributes["loading"] = "lazy";
        return image;
    }

    /// <summary>Creates one independently editable feature summary.</summary>
    private HtmlNode Feature(string heading, string body)
    {
        var article = Element("article", style: new HtmlStyle
        {
            Display = CssDisplay.Flex,
            FlexDirection = CssFlexDirection.Column,
            Gap = CssLength.Rem(0.75m),
            Padding = AllSpacing(CssLength.Rem(1.5m)),
            Surface = Surface("#f8fafc", CssLength.Rem(0.75m))
        });
        article.Children.Add(Element("h3", heading, Typography(color: "#111827", size: CssLength.Rem(1.25m), weight: 700)));
        article.Children.Add(Element("p", body, Typography(color: "#4b5563")));
        return article;
    }

    /// <summary>Creates one static question-and-answer pair.</summary>
    private HtmlNode Question(string heading, string body)
    {
        var article = Element("article", style: new HtmlStyle
        {
            Padding = AllSpacing(CssLength.Rem(1.5m)),
            Surface = Surface("#f8fafc", CssLength.Rem(0.75m))
        });
        article.Children.Add(Element("h3", heading, Typography(color: "#111827", size: CssLength.Rem(1.125m), weight: 700)));
        article.Children.Add(Element("p", body, Typography(color: "#4b5563")));
        return article;
    }

    /// <summary>Creates one feature-list item with a visible ordering marker.</summary>
    private HtmlNode FeatureListItem(string number, string heading, string body)
    {
        var article = Element("article", style: new HtmlStyle
        {
            Display = CssDisplay.Flex,
            FlexDirection = CssFlexDirection.Row,
            AlignItems = CssAlignment.Center,
            Gap = CssLength.Rem(1),
            Padding = AllSpacing(CssLength.Rem(1.25m)),
            Surface = Surface("#f8fafc", CssLength.Rem(0.75m))
        });
        article.Children.Add(Element("span", number, Typography(
            color: "#7c3aed", size: CssLength.Rem(1.25m), weight: 800)));
        var copy = Element("div");
        copy.Children.Add(Element("h3", heading, Typography(
            color: "#111827", size: CssLength.Rem(1.125m), weight: 700)));
        copy.Children.Add(Element("p", body, Typography(color: "#4b5563")));
        article.Children.Add(copy);
        return article;
    }

    /// <summary>Creates one native details disclosure with a summary and answer.</summary>
    private HtmlNode AccordionQuestion(string heading, string body)
    {
        var details = Element("details", style: new HtmlStyle
        {
            Padding = AllSpacing(CssLength.Rem(1.25m)),
            Surface = Surface("#ffffff", CssLength.Rem(0.75m))
        });
        details.Children.Add(Element("summary", heading, Typography(
            color: "#111827", size: CssLength.Rem(1.05m), weight: 700)));
        details.Children.Add(Element("p", body, Typography(color: "#4b5563")));
        return details;
    }

    /// <summary>Creates a styled anchor; URL safety remains enforced by validation and rendering policies.</summary>
    private HtmlNode Link(string text, string href, string backgroundColor, string textColor)
    {
        var link = Element("a", text, new HtmlStyle
        {
            Padding = new CssLogicalSpacing
            {
                BlockStart = CssLength.Rem(0.75m),
                InlineEnd = CssLength.Rem(1.25m),
                BlockEnd = CssLength.Rem(0.75m),
                InlineStart = CssLength.Rem(1.25m)
            },
            Surface = Surface(backgroundColor, CssLength.Rem(0.5m)),
            Typography = new CssTypographyStyle
            {
                Color = CssColor.Hex(textColor),
                FontWeight = 600,
                Alignment = CssTextAlignment.Center
            }
        });
        link.Attributes["href"] = href;
        return link;
    }

    /// <summary>
    /// Creates a catalog-backed element and optionally appends one literal text child.
    /// </summary>
    /// <exception cref="InvalidOperationException">The hard-coded template tag is absent from the active catalog.</exception>
    private HtmlNode Element(string tag, string? text = null, HtmlStyle? style = null)
    {
        var node = catalog.CreateElement(tag);
        node.Style = style;
        if (text is not null)
        {
            node.Children.Add(HtmlNode.CreateText(text));
        }

        return node;
    }

    /// <summary>Wraps optional typography values in the semantic style group expected by element templates.</summary>
    private static HtmlStyle Typography(
        string? color = null,
        CssLength? size = null,
        int? weight = null,
        CssTextAlignment? alignment = null) => new()
        {
            Typography = new CssTypographyStyle
            {
                Color = color is null ? null : CssColor.Hex(color),
                FontSize = size,
                FontWeight = weight,
                Alignment = alignment
            }
        };

    /// <summary>Creates a solid surface value with an optional uniform corner radius.</summary>
    private static CssSurfaceStyle Surface(string color, CssLength? radius = null) => new()
    {
        BackgroundColor = CssColor.Hex(color),
        BorderRadius = radius
    };

    /// <summary>Creates equal logical spacing on all four sides.</summary>
    private static CssLogicalSpacing AllSpacing(CssLength value) => new()
    {
        BlockStart = value,
        InlineEnd = value,
        BlockEnd = value,
        InlineStart = value
    };

    /// <summary>Creates equal spacing on the block axis while leaving inline spacing unspecified.</summary>
    private static CssLogicalSpacing Vertical(CssLength value) => new()
    {
        BlockStart = value,
        BlockEnd = value
    };
}
