using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Html;

/// <summary>
/// Creates curated components as ordinary, independently editable HTML nodes.
/// </summary>
public sealed class HtmlComponentTemplateFactory(HtmlElementCatalog catalog)
    : IHtmlComponentTemplateFactory
{
    public Result<HtmlNode> Create(HtmlComponentTemplateKind kind)
    {
        var component = kind switch
        {
            HtmlComponentTemplateKind.Hero => CreateHero(),
            HtmlComponentTemplateKind.FeatureGrid => CreateFeatureGrid(),
            HtmlComponentTemplateKind.CallToAction => CreateCallToAction(),
            HtmlComponentTemplateKind.FrequentlyAskedQuestions => CreateFrequentlyAskedQuestions(),
            HtmlComponentTemplateKind.Testimonial => CreateTestimonial(),
            HtmlComponentTemplateKind.Statistics => CreateStatistics(),
            HtmlComponentTemplateKind.ImageAndText => CreateImageAndText(),
            HtmlComponentTemplateKind.ContactForm => CreateContactForm(),
            HtmlComponentTemplateKind.Gallery => CreateGallery(),
            _ => null
        };

        return component is null
            ? new Result<HtmlNode>.Failure(
                AeroError.ValidationError(["The requested component template is not supported."]))
            : new Result<HtmlNode>.Ok(component);
    }

    private HtmlNode CreateHero()
    {
        var section = Element("section", style: new HtmlStyle
        {
            Display = CssDisplay.Flex,
            AlignItems = CssAlignment.Center,
            JustifyContent = CssJustification.Center,
            MinimumHeight = CssLength.ViewportHeight(65),
            Padding = All(CssLength.Rem(3)),
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

    private HtmlNode CreateFeatureGrid()
    {
        var section = Element("section", style: new HtmlStyle
        {
            Padding = All(CssLength.Rem(3)),
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
            Padding = All(CssLength.Rem(2.5m)),
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

    private HtmlNode CreateFrequentlyAskedQuestions()
    {
        var section = Element("section", style: new HtmlStyle
        {
            Padding = All(CssLength.Rem(3))
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

    private HtmlNode CreateTestimonial()
    {
        var section = Element("section", style: new HtmlStyle
        {
            Padding = All(CssLength.Rem(3)),
            Surface = Surface("#f8fafc", CssLength.Rem(1))
        });
        section.Children.Add(Element("h2", "What our customers say", Typography(
            color: "#111827", size: CssLength.Rem(2.25m), weight: 700, alignment: CssTextAlignment.Center)));

        var quote = Element("blockquote", style: new HtmlStyle
        {
            Margin = Vertical(CssLength.Rem(2)),
            Padding = All(CssLength.Rem(2)),
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

    private HtmlNode CreateStatistics()
    {
        var section = Element("section", style: new HtmlStyle
        {
            Padding = All(CssLength.Rem(3)),
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

    private HtmlNode CreateImageAndText()
    {
        var section = Element("section", style: new HtmlStyle
        {
            Display = CssDisplay.Flex,
            FlexDirection = CssFlexDirection.Row,
            StackOnSmallScreens = true,
            AlignItems = CssAlignment.Center,
            Gap = CssLength.Rem(2.5m),
            Padding = All(CssLength.Rem(3))
        });
        var figure = Element("figure");
        figure.Children.Add(Image("/media/image.jpg", "Describe the featured image"));
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

    private HtmlNode CreateContactForm()
    {
        var section = Element("section", style: new HtmlStyle
        {
            Display = CssDisplay.Grid,
            GridColumns = 2,
            StackOnSmallScreens = true,
            Gap = CssLength.Rem(2.5m),
            Padding = All(CssLength.Rem(3)),
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
            Padding = All(CssLength.Rem(1.5m)),
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
        form.Children.Add(messageLabel);
        form.Children.Add(message);

        var submit = Element("button", "Send message", new HtmlStyle
        {
            Padding = All(CssLength.Rem(0.75m)),
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

    private HtmlNode CreateGallery()
    {
        var section = Element("section", style: new HtmlStyle
        {
            Padding = All(CssLength.Rem(3))
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
        grid.Children.Add(GalleryFigure("/media/gallery-1.jpg", "Gallery image one"));
        grid.Children.Add(GalleryFigure("/media/gallery-2.jpg", "Gallery image two"));
        grid.Children.Add(GalleryFigure("/media/gallery-3.jpg", "Gallery image three"));
        section.Children.Add(grid);
        return section;
    }

    private HtmlNode Statistic(string value, string display, string label)
    {
        var article = Element("article", style: new HtmlStyle
        {
            Padding = All(CssLength.Rem(1.5m)),
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
        form.Children.Add(label);
        form.Children.Add(input);
    }

    private HtmlNode GalleryFigure(string source, string alternativeText)
    {
        var figure = Element("figure");
        figure.Children.Add(Image(source, alternativeText));
        figure.Children.Add(Element("figcaption", alternativeText));
        return figure;
    }

    private HtmlNode Image(string source, string alternativeText)
    {
        var image = catalog.CreateElement("img");
        image.Attributes["src"] = source;
        image.Attributes["alt"] = alternativeText;
        image.Attributes["loading"] = "lazy";
        return image;
    }

    private HtmlNode Feature(string heading, string body)
    {
        var article = Element("article", style: new HtmlStyle
        {
            Display = CssDisplay.Flex,
            FlexDirection = CssFlexDirection.Column,
            Gap = CssLength.Rem(0.75m),
            Padding = All(CssLength.Rem(1.5m)),
            Surface = Surface("#f8fafc", CssLength.Rem(0.75m))
        });
        article.Children.Add(Element("h3", heading, Typography(color: "#111827", size: CssLength.Rem(1.25m), weight: 700)));
        article.Children.Add(Element("p", body, Typography(color: "#4b5563")));
        return article;
    }

    private HtmlNode Question(string heading, string body)
    {
        var article = Element("article", style: new HtmlStyle
        {
            Padding = All(CssLength.Rem(1.5m)),
            Surface = Surface("#f8fafc", CssLength.Rem(0.75m))
        });
        article.Children.Add(Element("h3", heading, Typography(color: "#111827", size: CssLength.Rem(1.125m), weight: 700)));
        article.Children.Add(Element("p", body, Typography(color: "#4b5563")));
        return article;
    }

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

    private static CssSurfaceStyle Surface(string color, CssLength? radius = null) => new()
    {
        BackgroundColor = CssColor.Hex(color),
        BorderRadius = radius
    };

    private static CssLogicalSpacing All(CssLength value) => new()
    {
        BlockStart = value,
        InlineEnd = value,
        BlockEnd = value,
        InlineStart = value
    };

    private static CssLogicalSpacing Vertical(CssLength value) => new()
    {
        BlockStart = value,
        BlockEnd = value
    };
}
