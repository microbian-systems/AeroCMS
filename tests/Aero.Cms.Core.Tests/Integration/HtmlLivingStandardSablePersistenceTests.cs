using AeroDB.Sable;
using SurrealDb.Embedded.InMemory;
using SurrealDb.Net.Models;

namespace Aero.Cms.Core.Tests.Integration;

/// <summary>
/// A persistence spike for the proposed HTML Living Standard page model.
/// Each primitive derives from <see cref="SableDocument"/>, which implements
/// <see cref="ISableDocument{TId}"/> with a Snowflake-style long identity.
/// </summary>
public sealed class HtmlLivingStandardSablePersistenceTests
{
    [Test]
    public async Task Deeply_nested_html_primitives_round_trip_through_surrealdb()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<HtmlPageDocument>(SchemaMode.Flexible);
        await harness.InitializeAsync();

        var page = CreatePage();
        harness.Session.Store(page);
        await harness.Session.SaveChangesAsync();

        await using var verificationSession = await harness.OpenSessionAsync();
        var restored = await verificationSession.LoadAsync<HtmlPageDocument>(page.Id);

        await Assert.That(restored).IsNotNull();
        await Assert.That(restored!.Html.TagName).IsEqualTo("html");
        await Assert.That(restored.Html.Body.TagName).IsEqualTo("body");
        await Assert.That(restored.Html.Body.Sections).Count().IsEqualTo(1);

        var section = restored.Html.Body.Sections[0];
        await Assert.That(section.TagName).IsEqualTo("section");
        await Assert.That(section.Divs).Count().IsEqualTo(1);

        var content = section.Divs[0];
        await Assert.That(content.Classes).IsEqualTo("mx-auto max-w-4xl px-6 py-12");
        await Assert.That(content.Button.Text).IsEqualTo("Save changes");
        await Assert.That(content.Button.Attributes["type"]).IsEqualTo("button");
        await Assert.That(content.OrderedList.Items.Select(item => item.Text))
            .IsEquivalentTo(["Choose a section", "Add content", "Publish"]);
        await Assert.That(content.UnorderedList.Items.Select(item => item.Text))
            .IsEquivalentTo(["Tailwind classes", "Accessible markup", "No scripts"]);
    }

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
            Html = new GeneratedSableHtmlNode
            {
                TagName = "html",
                Attributes = new Dictionary<string, string> { ["lang"] = "en" },
                Children =
                [
                    new GeneratedSableHtmlNode
                    {
                        TagName = "body",
                        Classes = "bg-slate-50 text-slate-900",
                        Children =
                        [
                            new GeneratedSableHtmlNode
                            {
                                TagName = "section",
                                Children =
                                [
                                    new GeneratedSableHtmlNode
                                    {
                                        TagName = "div",
                                        Classes = "mx-auto max-w-4xl px-6 py-12",
                                        Children =
                                        [
                                            new GeneratedSableHtmlNode
                                            {
                                                TagName = "button",
                                                Text = "Save changes",
                                                Attributes = new Dictionary<string, string> { ["type"] = "button" }
                                            },
                                            new GeneratedSableHtmlNode
                                            {
                                                TagName = "ol",
                                                Children =
                                                [
                                                    new GeneratedSableHtmlNode { TagName = "li", Text = "Choose a section" },
                                                    new GeneratedSableHtmlNode { TagName = "li", Text = "Add content" }
                                                ]
                                            },
                                            new GeneratedSableHtmlNode
                                            {
                                                TagName = "ul",
                                                Children =
                                                [
                                                    new GeneratedSableHtmlNode { TagName = "li", Text = "Tailwind classes" },
                                                    new GeneratedSableHtmlNode { TagName = "li", Text = "No scripts" }
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

        harness.Session.Store(page);
        await harness.Session.SaveChangesAsync();

        await using var verificationSession = await harness.OpenSessionAsync();
        var restored = await verificationSession.LoadAsync<GeneratedSableHtmlPageDocument>(page.Id);

        await Assert.That(restored).IsNotNull();
        await Assert.That(restored!.Html.Children[0].Children[0].Children[0].Children[0].Text)
            .IsEqualTo("Save changes");
        await Assert.That(restored.Html.Children[0].Children[0].Children[0].Children[1].Children
            .Select(item => item.Text!))
            .IsEquivalentTo(["Choose a section", "Add content"]);
        await Assert.That(restored.Html.Children[0].Children[0].Children[0].Children[2].Children
            .Select(item => item.Text!))
            .IsEquivalentTo(["Tailwind classes", "No scripts"]);
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

    [Test]
    public async Task Direct_surrealdb_net_schemafull_record_with_flexible_html_field_round_trips()
    {
        await using var client = new SurrealDbMemoryClient();
        await client.Use("html_primitives_schemafull", "html_primitives_schemafull");
        await client.RawQuery("""
            DEFINE TABLE html_page SCHEMAFULL;
            DEFINE FIELD title ON TABLE html_page TYPE string;
            DEFINE FIELD html ON TABLE html_page TYPE object FLEXIBLE;
            """);

        var page = new SdkHtmlPageRecord
        {
            Id = new RecordIdOf<long>("html_page", 9_003),
            Title = "Schemafull direct SDK HTML primitive persistence spike",
            Html = new SdkHtmlNode
            {
                TagName = "html",
                Children =
                [
                    new SdkHtmlNode
                    {
                        TagName = "body",
                        Children =
                        [
                            new SdkHtmlNode
                            {
                                TagName = "section",
                                Children =
                                [
                                    new SdkHtmlNode
                                    {
                                        TagName = "div",
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
                                                    new SdkHtmlNode { TagName = "li", Text = "Add content" }
                                                ]
                                            },
                                            new SdkHtmlNode
                                            {
                                                TagName = "ul",
                                                Children =
                                                [
                                                    new SdkHtmlNode { TagName = "li", Text = "Tailwind classes" },
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

        await client.Create(page);
        var restored = await client.Select<SdkHtmlPageRecord>(page.Id!);

        await Assert.That(restored).IsNotNull();
        await Assert.That(restored!.Html.Children[0].Children[0].Children[0].Children[0].Text)
            .IsEqualTo("Save changes");
        await Assert.That(restored.Html.Children[0].Children[0].Children[0].Children[1].Children
            .Select(item => item.Text!))
            .IsEquivalentTo(["Choose a section", "Add content"]);
        await Assert.That(restored.Html.Children[0].Children[0].Children[0].Children[2].Children
            .Select(item => item.Text!))
            .IsEquivalentTo(["Tailwind classes", "No scripts"]);
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
    public GeneratedSableHtmlNode Html { get; set; } = new();
}

/// <summary>A nested value object, not an independently persisted record.</summary>
public sealed class GeneratedSableHtmlNode
{
    public string TagName { get; set; } = string.Empty;
    public string? Classes { get; set; }
    public string? Text { get; set; }
    public Dictionary<string, string> Attributes { get; set; } = [];
    public List<GeneratedSableHtmlNode> Children { get; set; } = [];
}
