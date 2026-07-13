using AeroDB.Sable;
using Aero.Cms.Html;
using SurrealDb.Embedded.InMemory;
using SurrealDb.Net.Models;

namespace Aero.Cms.Core.Tests.Integration;

/// <summary>
/// Persistence spikes retained for the accepted flat, recursive HTML value-object model.
/// </summary>
public sealed class HtmlLivingStandardSablePersistenceTests
{
    [Test]
    public async Task Sable_schemaless_root_document_with_plain_nested_html_nodes_round_trips()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<GeneratedSableHtmlPageDocument>(SchemaMode.Flexible);
        await harness.InitializeAsync();

        var page = new GeneratedSableHtmlPageDocument
        {
            Id = 9_015,
            Title = "Sable HTML value-object persistence spike",
            Content = CreateSharedHtmlContent()
        };

        harness.Session.Store(page);
        await harness.Session.SaveChangesAsync();

        await using var verificationSession = await harness.OpenSessionAsync();
        var restored = await verificationSession.LoadAsync<GeneratedSableHtmlPageDocument>(page.Id);

        await Assert.That(restored).IsNotNull();
        await Assert.That(restored!.Content.Root.Children[0].Children[0].Children[0].Children[0].Children[0].Children[0].Text)
            .IsEqualTo("Save changes");
        await Assert.That(restored.Content.Root.Children[0].Children[0].Children[0].Children[0].Children[1].Children
            .Select(item => item.Children[0].Text!))
            .IsEquivalentTo(["Choose a section", "Add content"]);
        await Assert.That(restored.Content.Root.Children[0].Children[0].Children[0].Children[0].Children[2].Children
            .Select(item => item.Children[0].Text!))
            .IsEquivalentTo(["Tailwind classes", "No scripts"]);
        var restoredSection = restored.Content.Root.Children[0].Children[0].Children[0];
        await Assert.That(restoredSection.Style!.Display).IsEqualTo(CssDisplay.Grid);
        await Assert.That(restoredSection.Style.GridColumns).IsEqualTo(2);
        await Assert.That(restoredSection.Style.StackOnSmallScreens).IsTrue();
        await Assert.That(restoredSection.Style.Gap!.Value).IsEqualTo(1.5m);
        await Assert.That(restoredSection.Style.Gap.Unit).IsEqualTo(CssLengthUnit.Rem);
        await Assert.That(restoredSection.Style.Padding!.InlineStart!.Value).IsEqualTo(2m);
        await Assert.That(restoredSection.Style.Surface!.BackgroundColor!.Kind).IsEqualTo(CssColorKind.ThemeToken);
        await Assert.That(restoredSection.Style.Surface.BackgroundColor.Value).IsEqualTo("surface.brand");
        await Assert.That(restoredSection.Style.Surface.BackgroundImageUrl).IsEqualTo("/media/page-hero.jpg");
        await Assert.That(restoredSection.Style.Surface.OverlayOpacity).IsEqualTo(0.35m);
        await Assert.That(restoredSection.Style.Surface.BorderRadius!.Value).IsEqualTo(1.25m);
        var restoredButton = restoredSection.Children[0].Children[0];
        await Assert.That(restoredButton.Style!.Typography!.FontWeight).IsEqualTo(700);
        await Assert.That(restoredButton.Style.Typography.Alignment).IsEqualTo(CssTextAlignment.Center);
        await Assert.That(restoredButton.Style.Typography.Gradient!.StartColor.Value).IsEqualTo("text.hero.start");
        await Assert.That(restoredButton.Style.Typography.Gradient.EndColor.Value).IsEqualTo("#ffffff");
        await Assert.That(restoredButton.Style.Typography.Gradient.AngleDegrees).IsEqualTo(120m);
    }

    [Test]
    public async Task Direct_surrealdb_net_schemaless_record_with_nested_html_values_round_trips()
    {
        await using var client = new SurrealDbMemoryClient();
        await client.Use("html_primitives", "html_primitives");

        var page = new SdkHtmlPageRecord
        {
            Id = new RecordIdOf<long>("html_page", 9_002),
            Title = "Direct SDK HTML primitive persistence spike",
            Html = new SdkHtmlNode
            {
                TagName = "html",
                Attributes = new Dictionary<string, string> { ["lang"] = "en" },
                Children =
                [
                    new SdkHtmlNode
                    {
                        TagName = "body",
                        Classes = "bg-slate-50 text-slate-900",
                        Children =
                        [
                            new SdkHtmlNode
                            {
                                TagName = "section",
                                Attributes = new Dictionary<string, string> { ["aria-label"] = "Page editor proof" },
                                Children =
                                [
                                    new SdkHtmlNode
                                    {
                                        TagName = "div",
                                        Classes = "mx-auto max-w-4xl px-6 py-12",
                                        Children =
                                        [
                                            new SdkHtmlNode
                                            {
                                                TagName = "button",
                                                Text = "Save changes",
                                                Attributes = new Dictionary<string, string> { ["type"] = "button" }
                                            },
                                            new SdkHtmlNode
                                            {
                                                TagName = "ol",
                                                Children =
                                                [
                                                    new SdkHtmlNode { TagName = "li", Text = "Choose a section" },
                                                    new SdkHtmlNode { TagName = "li", Text = "Add content" },
                                                    new SdkHtmlNode { TagName = "li", Text = "Publish" }
                                                ]
                                            },
                                            new SdkHtmlNode
                                            {
                                                TagName = "ul",
                                                Children =
                                                [
                                                    new SdkHtmlNode { TagName = "li", Text = "Tailwind classes" },
                                                    new SdkHtmlNode { TagName = "li", Text = "Accessible markup" },
                                                    new SdkHtmlNode { TagName = "li", Text = "No scripts" }
                                                ]
                                            }
                                        ]
                                    }
                                ]
                            }
                        ]
                    }
                ]
            }
        };

        var created = await client.Create(page);
        var restored = await client.Select<SdkHtmlPageRecord>(page.Id!);

        await Assert.That(created.Id).IsNotNull();
        await Assert.That(created.Id!.Table).IsEqualTo("html_page");
        await Assert.That(restored).IsNotNull();
        await Assert.That(restored!.Html.Children[0].Children[0].Children[0].Children[0].Text)
            .IsEqualTo("Save changes");
        await Assert.That(restored.Html.Children[0].Children[0].Children[0].Children[1].Children
            .Select(item => item.Text!))
            .IsEquivalentTo(["Choose a section", "Add content", "Publish"]);
    }

    private static HtmlPageDocument CreatePage() => new()
    {
        Id = 9_001,
        Title = "HTML primitive persistence spike",
        Html = new HtmlElementDocument
        {
            Lang = "en",
            Body = new BodyElementDocument
            {
                Classes = "bg-slate-50 text-slate-900",
                Sections =
                [
                    new SectionElementDocument
                    {
                        Id = 9_004,
                        Attributes = new Dictionary<string, string> { ["aria-label"] = "Page editor proof" },
                        Divs =
                        [
                            new DivElementDocument
                            {
                                Id = 9_005,
                                Classes = "mx-auto max-w-4xl px-6 py-12",
                                Button = new ButtonElementDocument
                                {
                                    Id = 9_006,
                                    Classes = "rounded bg-indigo-600 px-4 py-2 text-white",
                                    Text = "Save changes",
                                    Attributes = new Dictionary<string, string> { ["type"] = "button" }
                                },
                                OrderedList = new OrderedListElementDocument
                                {
                                    Id = 9_007,
                                    Classes = "list-decimal pl-6",
                                    Items =
                                    [
                                        new ListItemElementDocument { Id = 9_008, Text = "Choose a section" },
                                        new ListItemElementDocument { Id = 9_009, Text = "Add content" },
                                        new ListItemElementDocument { Id = 9_010, Text = "Publish" }
                                    ]
                                },
                                UnorderedList = new UnorderedListElementDocument
                                {
                                    Id = 9_011,
                                    Classes = "list-disc pl-6",
                                    Items =
                                    [
                                        new ListItemElementDocument { Id = 9_012, Text = "Tailwind classes" },
                                        new ListItemElementDocument { Id = 9_013, Text = "Accessible markup" },
                                        new ListItemElementDocument { Id = 9_014, Text = "No scripts" }
                                    ]
                                }
                            }
                        ]
                    }
                ]
            }
        }
    };

    private static HtmlPageContent CreateSharedHtmlContent()
    {
        var root = HtmlNode.CreateFragment();
        var html = HtmlNode.CreateElement("html");
        html.Attributes["lang"] = "en";
        var body = HtmlNode.CreateElement("body");
        body.ThemeClasses.AddRange(["bg-slate-50", "text-slate-900"]);
        var section = HtmlNode.CreateElement("section");
        section.Style = new HtmlStyle
        {
            Display = CssDisplay.Grid,
            GridColumns = 2,
            StackOnSmallScreens = true,
            Gap = CssLength.Rem(1.5m),
            Padding = new CssLogicalSpacing { InlineStart = CssLength.Rem(2) },
            Surface = new CssSurfaceStyle
            {
                BackgroundColor = CssColor.Token("surface.brand"),
                BackgroundImageUrl = "/media/page-hero.jpg",
                OverlayColor = CssColor.Hex("#000000"),
                OverlayOpacity = 0.35m,
                BackgroundFit = CssBackgroundFit.Cover,
                BackgroundPosition = CssBackgroundPosition.Center,
                BackgroundRepeat = CssBackgroundRepeat.NoRepeat,
                BorderRadius = CssLength.Rem(1.25m)
            }
        };
        var content = HtmlNode.CreateElement("div");
        content.ThemeClasses.AddRange(["mx-auto", "max-w-4xl", "px-6", "py-12"]);

        var button = HtmlNode.CreateElement("button");
        button.Attributes["type"] = "button";
        button.Style = new HtmlStyle
        {
            Typography = new CssTypographyStyle
            {
                FontSize = CssLength.Rem(3),
                FontWeight = 700,
                LineHeight = 1.1m,
                Alignment = CssTextAlignment.Center,
                Gradient = new CssTextGradient
                {
                    StartColor = CssColor.Token("text.hero.start"),
                    EndColor = CssColor.Hex("#ffffff"),
                    AngleDegrees = 120
                }
            }
        };
        button.Children.Add(HtmlNode.CreateText("Save changes"));

        var orderedList = HtmlNode.CreateElement("ol");
        orderedList.Children.Add(CreateListItem("Choose a section"));
        orderedList.Children.Add(CreateListItem("Add content"));

        var unorderedList = HtmlNode.CreateElement("ul");
        unorderedList.Children.Add(CreateListItem("Tailwind classes"));
        unorderedList.Children.Add(CreateListItem("No scripts"));

        content.Children.AddRange([button, orderedList, unorderedList]);
        section.Children.Add(content);
        body.Children.Add(section);
        html.Children.Add(body);
        root.Children.Add(html);

        return new HtmlPageContent { Root = root };
    }

    private static HtmlNode CreateListItem(string text)
    {
        var item = HtmlNode.CreateElement("li");
        item.Children.Add(HtmlNode.CreateText(text));
        return item;
    }

    private abstract class HtmlPrimitiveDocument : SableDocument
    {
        public abstract string TagName { get; }
        public string? Classes { get; set; }
        public Dictionary<string, string> Attributes { get; set; } = [];
    }

    private sealed class HtmlPageDocument : SableDocument
    {
        public string Title { get; set; } = string.Empty;
        public HtmlElementDocument Html { get; set; } = new();
    }

    private sealed class HtmlElementDocument : HtmlPrimitiveDocument
    {
        public override string TagName => "html";
        public string Lang { get; set; } = "en";
        public BodyElementDocument Body { get; set; } = new();
    }

    private sealed class BodyElementDocument : HtmlPrimitiveDocument
    {
        public override string TagName => "body";
        public List<SectionElementDocument> Sections { get; set; } = [];
    }

    private sealed class SectionElementDocument : HtmlPrimitiveDocument
    {
        public override string TagName => "section";
        public List<DivElementDocument> Divs { get; set; } = [];
    }

    private sealed class DivElementDocument : HtmlPrimitiveDocument
    {
        public override string TagName => "div";
        public ButtonElementDocument Button { get; set; } = new();
        public OrderedListElementDocument OrderedList { get; set; } = new();
        public UnorderedListElementDocument UnorderedList { get; set; } = new();
    }

    private sealed class ButtonElementDocument : HtmlPrimitiveDocument
    {
        public override string TagName => "button";
        public string Text { get; set; } = string.Empty;
    }

    private sealed class OrderedListElementDocument : HtmlPrimitiveDocument
    {
        public override string TagName => "ol";
        public List<ListItemElementDocument> Items { get; set; } = [];
    }

    private sealed class UnorderedListElementDocument : HtmlPrimitiveDocument
    {
        public override string TagName => "ul";
        public List<ListItemElementDocument> Items { get; set; } = [];
    }

    private sealed class ListItemElementDocument : HtmlPrimitiveDocument
    {
        public override string TagName => "li";
        public string Text { get; set; } = string.Empty;
    }

    // SurrealDb.Net's official typed-record base. RecordOf<long> is not present in v0.10.2;
    // the strongly typed long identity is represented by RecordIdOf<long> on Record.Id.
    private sealed class SdkHtmlPageRecord : Record
    {
        public string Title { get; set; } = string.Empty;
        public SdkHtmlNode Html { get; set; } = new();
    }

    private sealed class SdkHtmlNode
    {
        public string TagName { get; set; } = string.Empty;
        public string? Classes { get; set; }
        public string? Text { get; set; }
        public Dictionary<string, string> Attributes { get; set; } = [];
        public List<SdkHtmlNode> Children { get; set; } = [];
    }
}

/// <summary>
/// Public top-level test document so AeroDB's consumer source generator can emit
/// the same metadata and CBOR shim that a production CMS document receives.
/// </summary>
public sealed class GeneratedSableHtmlPageDocument : SableDocument
{
    public string Title { get; set; } = string.Empty;
    public HtmlPageContent Content { get; set; } = new();
}
