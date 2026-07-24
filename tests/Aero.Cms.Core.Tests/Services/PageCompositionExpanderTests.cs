using System.Collections.Immutable;
using System.Text.Json;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Content.Composition;
using Aero.Cms.Abstractions.Pages.Composition;
using Aero.Cms.Html;
using Aero.Cms.Core.Content.Templating;
using Aero.Cms.Modules.Pages.Rendering;
using Aero.Core;
using Aero.Core.Railway;
using NSubstitute;
using Shouldly;

namespace Aero.Cms.Core.Tests.Services;

public sealed class PageCompositionExpanderTests
{
    [Test]
    public async Task ExpandAsync_clones_list_templates_binds_each_item_and_preserves_the_saved_tree()
    {
        var (content, composition, scopeId, templateId, bindingId) = CreateListDocument();
        var resolver = Substitute.For<IContentCompositionResolver>();
        resolver.ResolveListAsync(42, "en-US", Arg.Any<PageContentListScope>(), 2, Arg.Any<CancellationToken>())
            .Returns(Prelude.Ok<PublishedContentPage, AeroError>(new PublishedContentPage
            {
                ContentTypeAlias = "articles",
                PageNumber = 2,
                PageSize = 2,
                TotalCount = 4,
                Items = [Projection(1, "<First>"), Projection(2, "Second")]
            }));
        var expander = new PageCompositionExpander(resolver, CreateValidator());

        var result = await expander.ExpandAsync(
            42,
            "en-US",
            content,
            composition,
            new Dictionary<long, int> { [scopeId] = 2 });

        result.IsSuccess.ShouldBeTrue();
        var expansion = ((Result<PageCompositionExpansion, AeroError>.Ok)result).Value;
        var expanded = expansion.Content;
        expansion.ContentTypeAliases.ShouldBe(["articles"]);
        var expandedScope = HtmlTreeOperations.FindById(expanded.Root, scopeId)!;
        expandedScope.Children.Count.ShouldBe(2);
        expandedScope.Children.Select(node => node.NodeId).Distinct().Count().ShouldBe(2);
        expandedScope.Children[0].Children.Single().Children.Single().Text.ShouldBe("<First>");
        expandedScope.Children[1].Children.Single().Children.Single().Text.ShouldBe("Second");
        HtmlTreeOperations.HasUniqueNodeIds(expanded.Root).ShouldBeTrue();

        var savedTemplate = HtmlTreeOperations.FindById(content.Root, templateId)!;
        savedTemplate.Children.Single().NodeId.ShouldBe(bindingId);
        savedTemplate.Children.Single().Children.Single().Text.ShouldBe("Placeholder");
    }

    [Test]
    public async Task ExpandAsync_removes_an_empty_render_nothing_scope()
    {
        var (content, composition, scopeId, _, _) = CreateListDocument();
        var resolver = Substitute.For<IContentCompositionResolver>();
        resolver.ResolveListAsync(42, "en-US", Arg.Any<PageContentListScope>(), 1, Arg.Any<CancellationToken>())
            .Returns(Prelude.Ok<PublishedContentPage, AeroError>(new PublishedContentPage
            {
                ContentTypeAlias = "articles"
            }));
        var expander = new PageCompositionExpander(resolver, CreateValidator());

        var result = await expander.ExpandAsync(42, "en-US", content, composition);

        result.IsSuccess.ShouldBeTrue();
        var expanded = ((Result<PageCompositionExpansion, AeroError>.Ok)result).Value.Content;
        HtmlTreeOperations.FindById(expanded.Root, scopeId).ShouldBeNull();
        HtmlTreeOperations.FindById(content.Root, scopeId).ShouldNotBeNull();
    }

    [Test]
    public async Task ExpandAsync_rejects_an_unsafe_bound_url_before_static_rendering()
    {
        var catalog = HtmlElementCatalog.CreateDefault();
        var scope = catalog.CreateElement("section");
        var link = catalog.CreateElement("a");
        link.Attributes["href"] = "#";
        link.Children.Add(HtmlNode.CreateText("Link"));
        scope.Children.Add(link);
        var content = new HtmlPageContent();
        content.Root.Children.Add(scope);
        var composition = new PageCompositionDocument
        {
            ContentItems =
            [
                new PageContentItemScope
                {
                    NodeId = scope.NodeId,
                    ContentTypeId = 501,
                    ContentTypeAlias = "articles",
                    ContentItemId = 1
                }
            ],
            FieldBindings =
            [
                new PageFieldBinding
                {
                    NodeId = link.NodeId,
                    ScopeNodeId = scope.NodeId,
                    FieldName = "url",
                    Target = PageFieldBindingTarget.Hyperlink
                }
            ]
        };
        var resolver = Substitute.For<IContentCompositionResolver>();
        resolver.ResolveItemAsync(42, "en-US", Arg.Any<PageContentItemScope>(), Arg.Any<CancellationToken>())
            .Returns(Prelude.Ok<PublishedContentItemProjection, AeroError>(new PublishedContentItemProjection
            {
                Id = 1,
                ContentTypeAlias = "articles",
                Slug = "one",
                Culture = "en-US",
                Fields = new Dictionary<string, JsonElement>
                {
                    ["url"] = JsonSerializer.SerializeToElement("javascript:alert(1)")
                }
            }));
        var expander = new PageCompositionExpander(resolver, CreateValidator(catalog));

        var result = await expander.ExpandAsync(42, "en-US", content, composition);

        result.IsFailure.ShouldBeTrue();
        HtmlTreeOperations.FindById(content.Root, link.NodeId)!.Attributes["href"].ShouldBe("#");
    }

    [Test]
    public async Task ExpandAsync_renders_markdown_before_cloning_an_enclosing_content_list()
    {
        var catalog = HtmlElementCatalog.CreateDefault();
        var scope = catalog.CreateElement("section");
        var template = catalog.CreateElement("article");
        var markdownTarget = catalog.CreateElement("div");
        markdownTarget.Children.Add(HtmlNode.CreateText("Editor placeholder"));
        var boundTitle = catalog.CreateElement("p");
        boundTitle.Children.Add(HtmlNode.CreateText("Title placeholder"));
        template.Children.Add(markdownTarget);
        template.Children.Add(boundTitle);
        scope.Children.Add(template);
        var content = new HtmlPageContent();
        content.Root.Children.Add(scope);
        var composition = new PageCompositionDocument
        {
            ContentLists =
            [
                new PageContentListScope
                {
                    NodeId = scope.NodeId,
                    ContentTypeId = 501,
                    ContentTypeAlias = "articles",
                    TemplateRootNodeId = template.NodeId,
                    Query = new PageContentListQuery { PageSize = 2 }
                }
            ],
            FieldBindings =
            [
                new PageFieldBinding
                {
                    NodeId = boundTitle.NodeId,
                    ScopeNodeId = scope.NodeId,
                    FieldName = "title",
                    Target = PageFieldBindingTarget.TextContent
                }
            ],
            RenderedFragments =
            [
                new PageRenderedFragment
                {
                    NodeId = markdownTarget.NodeId,
                    Kind = PageRenderedFragmentKind.Markdown,
                    Source = "## Details\n\n<script>alert('no')</script>"
                }
            ]
        };
        var resolver = Substitute.For<IContentCompositionResolver>();
        resolver.ResolveListAsync(42, "en-US", Arg.Any<PageContentListScope>(), 1, Arg.Any<CancellationToken>())
            .Returns(Prelude.Ok<PublishedContentPage, AeroError>(new PublishedContentPage
            {
                ContentTypeAlias = "articles",
                Items = [Projection(1, "First"), Projection(2, "Second")]
            }));
        var validator = CreateValidator(catalog);
        var markdown = new MarkdownInterchangeAdapter(
            new HtmlFragmentImporter(
                catalog,
                new HtmlAttributePolicy(),
                new HtmlContentModelPolicy(catalog),
                validator),
            validator);
        var expander = new PageCompositionExpander(
            resolver,
            validator,
            [new MarkdownPageFragmentRenderer(markdown)]);

        var result = await expander.ExpandAsync(42, "en-US", content, composition);

        result.IsSuccess.ShouldBeTrue();
        var expandedScope = HtmlTreeOperations.FindById(
            ((Result<PageCompositionExpansion, AeroError>.Ok)result).Value.Content.Root,
            scope.NodeId)!;
        expandedScope.Children.Count.ShouldBe(2);
        foreach (var article in expandedScope.Children)
        {
            var fragment = article.Children[0];
            fragment.Children[0].TagName.ShouldBe("h2");
            Descendants(fragment).Any(node => node.TagName == "script").ShouldBeFalse();
            Descendants(fragment).Any(node => node.Kind == HtmlNodeKind.Text
                && node.Text!.Contains("<script>", StringComparison.Ordinal)).ShouldBeTrue();
        }

        markdownTarget.Children.Single().Text.ShouldBe("Editor placeholder");
    }

    [Test]
    public async Task ExpandAsync_fails_closed_when_a_fragment_renderer_is_not_registered()
    {
        var catalog = HtmlElementCatalog.CreateDefault();
        var target = catalog.CreateElement("section");
        var content = new HtmlPageContent();
        content.Root.Children.Add(target);
        var composition = new PageCompositionDocument
        {
            RenderedFragments =
            [
                new PageRenderedFragment
                {
                    NodeId = target.NodeId,
                    Kind = PageRenderedFragmentKind.Markdown,
                    Source = "# Missing renderer"
                }
            ]
        };
        var expander = new PageCompositionExpander(
            Substitute.For<IContentCompositionResolver>(),
            CreateValidator(catalog));

        var result = await expander.ExpandAsync(42, "en-US", content, composition);

        result.IsFailure.ShouldBeTrue();
        target.Children.ShouldBeEmpty();
    }

    [Test]
    public async Task ExpandAsync_imports_valid_custom_html_without_mutating_saved_source_tree()
    {
        var catalog = HtmlElementCatalog.CreateDefault();
        var target = catalog.CreateElement("section");
        target.Children.Add(HtmlNode.CreateText("Editor placeholder"));
        var content = new HtmlPageContent();
        content.Root.Children.Add(target);
        var composition = new PageCompositionDocument
        {
            RenderedFragments =
            [
                new PageRenderedFragment
                {
                    NodeId = target.NodeId,
                    Kind = PageRenderedFragmentKind.CustomHtml,
                    Source = "<p><strong>Safe</strong> content</p>"
                }
            ]
        };
        var validator = CreateValidator(catalog);
        var importer = new HtmlFragmentImporter(
            catalog,
            new HtmlAttributePolicy(),
            new HtmlContentModelPolicy(catalog),
            validator);
        var expander = new PageCompositionExpander(
            Substitute.For<IContentCompositionResolver>(),
            validator,
            [new CustomHtmlPageFragmentRenderer(importer)]);

        var result = await expander.ExpandAsync(42, "en-US", content, composition);

        result.IsSuccess.ShouldBeTrue();
        var expandedTarget = HtmlTreeOperations.FindById(
            ((Result<PageCompositionExpansion, AeroError>.Ok)result).Value.Content.Root,
            target.NodeId)!;
        expandedTarget.Children.Single().TagName.ShouldBe("p");
        expandedTarget.Children.Single().Children.Single(node => node.Kind == HtmlNodeKind.Element)
            .TagName.ShouldBe("strong");
        target.Children.Single().Text.ShouldBe("Editor placeholder");
    }

    [Test]
    public async Task Custom_html_renderer_rejects_scripts_event_handlers_and_unsafe_urls()
    {
        var catalog = HtmlElementCatalog.CreateDefault();
        var policy = new HtmlContentModelPolicy(catalog);
        var validator = CreateValidator(catalog);
        var renderer = new CustomHtmlPageFragmentRenderer(new HtmlFragmentImporter(
            catalog,
            new HtmlAttributePolicy(),
            policy,
            validator));

        var context = new PageFragmentRenderContext { SiteId = 42, Culture = "en-US" };
        var script = await renderer.RenderAsync(new PageRenderedFragment
        {
            Kind = PageRenderedFragmentKind.CustomHtml,
            Source = "<script>alert(1)</script>"
        }, context);
        var eventHandler = await renderer.RenderAsync(new PageRenderedFragment
        {
            Kind = PageRenderedFragmentKind.CustomHtml,
            Source = "<button onclick=\"alert(1)\">Click</button>"
        }, context);
        var unsafeUrl = await renderer.RenderAsync(new PageRenderedFragment
        {
            Kind = PageRenderedFragmentKind.CustomHtml,
            Source = "<a href=\"javascript:alert(1)\">Click</a>"
        }, context);

        await Assert.That(script).IsTypeOf<Result<HtmlPageContent>.Failure>();
        await Assert.That(eventHandler).IsTypeOf<Result<HtmlPageContent>.Failure>();
        await Assert.That(unsafeUrl).IsTypeOf<Result<HtmlPageContent>.Failure>();
    }

    [Test]
    public async Task ExpandAsync_renders_scriban_with_explicit_page_and_site_context()
    {
        var catalog = HtmlElementCatalog.CreateDefault();
        var target = catalog.CreateElement("section");
        target.Children.Add(HtmlNode.CreateText("Editor placeholder"));
        var content = new HtmlPageContent();
        content.Root.Children.Add(target);
        var composition = new PageCompositionDocument
        {
            RenderedFragments =
            [
                new PageRenderedFragment
                {
                    NodeId = target.NodeId,
                    Kind = PageRenderedFragmentKind.Scriban,
                    Source = "<h2>{{ page.title }}</h2><p>{{ page.culture }} / {{ site.id }}</p>"
                }
            ]
        };
        var validator = CreateValidator(catalog);
        var importer = new HtmlFragmentImporter(
            catalog,
            new HtmlAttributePolicy(),
            new HtmlContentModelPolicy(catalog),
            validator);
        var expander = new PageCompositionExpander(
            Substitute.For<IContentCompositionResolver>(),
            validator,
            [new ScribanPageFragmentRenderer(new SecureScribanRenderer(), importer)]);

        var result = await expander.ExpandAsync(
            42,
            "en-US",
            content,
            composition,
            fragmentContext: new PageFragmentRenderContext
            {
                SiteId = 42,
                Culture = "en-US",
                PageId = 901,
                Title = "About Aero",
                Slug = "about",
                Path = "/about"
            });

        result.IsSuccess.ShouldBeTrue();
        var expandedTarget = HtmlTreeOperations.FindById(
            ((Result<PageCompositionExpansion, AeroError>.Ok)result).Value.Content.Root,
            target.NodeId)!;
        expandedTarget.Children[0].TagName.ShouldBe("h2");
        expandedTarget.Children[0].Children.Single().Text.ShouldBe("About Aero");
        expandedTarget.Children[1].Children.Single().Text.ShouldBe("en-US / 42");
        target.Children.Single().Text.ShouldBe("Editor placeholder");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ExpandAsync_renders_mixed_author_fragments_for_public_and_preview_pages(
        bool isPreview)
    {
        var catalog = HtmlElementCatalog.CreateDefault();
        var content = new HtmlPageContent();
        var fragments = new List<PageRenderedFragment>();

        AddFragment(
            PageRenderedFragmentKind.CustomHtml,
            "<p>Custom <strong>HTML</strong> block</p>");
        AddFragment(
            PageRenderedFragmentKind.Scriban,
            "<p>Site {{ site.id }} · {{ page.culture }}</p>");
        AddFragment(
            PageRenderedFragmentKind.Htmx,
            """
            <section>
              <button type="button" hx-get="/api/example" hx-target="#htmx-result">Load content</button>
              <div id="htmx-result" aria-live="polite"></div>
            </section>
            """);
        AddFragment(
            PageRenderedFragmentKind.SharpTs,
            """
            export function render(context: any) {
                return html`<p>${context.page.title}</p>`;
            }
            """);
        AddFragment(
            PageRenderedFragmentKind.Markdown,
            "## Markdown block\n\nStart writing here.");

        var validator = CreateValidator(catalog);
        var importer = new HtmlFragmentImporter(
            catalog,
            new HtmlAttributePolicy(),
            new HtmlContentModelPolicy(catalog),
            validator);
        var expander = new PageCompositionExpander(
            Substitute.For<IContentCompositionResolver>(),
            validator,
            [
                new CustomHtmlPageFragmentRenderer(importer),
                new ScribanPageFragmentRenderer(new SecureScribanRenderer(), importer),
                new HtmxPageFragmentRenderer(importer),
                new SharpTsPageFragmentRenderer(new SharpTsExecutor(), importer),
                new MarkdownPageFragmentRenderer(
                    new MarkdownInterchangeAdapter(importer, validator))
            ]);

        var result = await expander.ExpandAsync(
            1_529_706_005_277_655_041,
            "en-US",
            content,
            new PageCompositionDocument { RenderedFragments = fragments },
            fragmentContext: new PageFragmentRenderContext
            {
                SiteId = 1_529_706_005_277_655_041,
                Culture = "en-US",
                PageId = 1_530_221_140_281_556_994,
                Title = "Troy",
                Slug = "troy",
                Path = "/troy",
                IsPreview = isPreview
            });

        if (result is Result<PageCompositionExpansion, AeroError>.Failure failure)
        {
            throw new InvalidOperationException(FormatError(failure.Error));
        }

        var expansion = ((Result<PageCompositionExpansion, AeroError>.Ok)result).Value;
        expansion.Content.Root.Children.Count.ShouldBe(5);
        expansion.Content.Root.Children[0].Children.Single().TagName.ShouldBe("p");
        expansion.Content.Root.Children[1].Children.Single().TagName.ShouldBe("p");
        expansion.Content.Root.Children[2].Children.Single().TagName.ShouldBe("section");
        expansion.Content.Root.Children[3].Children.Single().TagName.ShouldBe("p");
        expansion.Content.Root.Children[4].Children[0].TagName.ShouldBe("h2");

        var compiled = new NativeCssStyleCompiler().Compile(
            expansion.Content,
            new NativeStyleProfile());
        if (compiled is Result<CompiledPageStyles>.Failure compileFailure)
        {
            throw new InvalidOperationException(FormatError(compileFailure.Error));
        }

        var rendered = new HtmlStaticRenderer(
            catalog,
            new HtmlContentModelPolicy(catalog),
            new HtmlAttributePolicy(),
            validator).RenderPage(
                expansion.Content,
                ((Result<CompiledPageStyles>.Ok)compiled).Value);
        if (rendered is Result<RenderedHtmlPage>.Failure renderFailure)
        {
            throw new InvalidOperationException(FormatError(renderFailure.Error));
        }

        void AddFragment(PageRenderedFragmentKind kind, string source)
        {
            var target = catalog.CreateElement("section");
            target.Children.Add(HtmlNode.CreateText($"{kind} block — double-click to edit"));
            content.Root.Children.Add(target);
            fragments.Add(new PageRenderedFragment
            {
                NodeId = target.NodeId,
                Kind = kind,
                Source = source
            });
        }
    }

    [Test]
    public async Task ExpandAsync_exposes_eager_hierarchy_to_scriban_with_string_ids()
    {
        var catalog = HtmlElementCatalog.CreateDefault();
        var target = catalog.CreateElement("section");
        var content = new HtmlPageContent();
        content.Root.Children.Add(target);
        var composition = new PageCompositionDocument
        {
            RenderedFragments =
            [
                new PageRenderedFragment
                {
                    NodeId = target.NodeId,
                    Kind = PageRenderedFragmentKind.Scriban,
                    Source = "{{ for topic in content.topics.roots }}<p>{{ topic.id }}|{{ topic.title }}|{{ topic.fields.summary }}|{{ topic.children[0].id }}</p>{{ end }}"
                }
            ]
        };
        var fields = ImmutableDictionary<string, JsonElement>.Empty
            .Add("summary", JsonSerializer.SerializeToElement("hello"));
        var child = new ContentNode(
            "9007199254740995",
            "topics",
            "Child",
            "child",
            ImmutableDictionary<string, JsonElement>.Empty,
            []);
        var root = new ContentNode(
            "9007199254740993",
            "topics",
            "Root",
            "root",
            fields,
            [child]);
        var query = new ContentQueryResult(
            "topics",
            "topics",
            [root],
            2,
            false);
        var queryResolution = new PageContentQueryResolution
        {
            Results = ImmutableDictionary<string, ContentQueryResult>.Empty
                .WithComparers(StringComparer.OrdinalIgnoreCase)
                .Add("topics", query),
            ContentTypeAliases = ["topics"]
        };
        var validator = CreateValidator(catalog);
        var importer = new HtmlFragmentImporter(
            catalog,
            new HtmlAttributePolicy(),
            new HtmlContentModelPolicy(catalog),
            validator);
        var expander = new PageCompositionExpander(
            Substitute.For<IContentCompositionResolver>(),
            validator,
            [new ScribanPageFragmentRenderer(new SecureScribanRenderer(), importer)]);

        var result = await expander.ExpandAsync(
            42,
            "en-US",
            content,
            composition,
            fragmentContext: new PageFragmentRenderContext
            {
                SiteId = 42,
                Culture = "en-US",
                ContentQueries = queryResolution
            });

        var success = result.ShouldBeOfType<Result<PageCompositionExpansion, AeroError>.Ok>();
        var expandedTarget = HtmlTreeOperations.FindById(
            success.Value.Content.Root,
            target.NodeId)!;
        expandedTarget.Children.Single().Children.Single().Text.ShouldBe(
            "9007199254740993|Root|hello|9007199254740995");
    }

    [Test]
    public async Task Scriban_renderer_rejects_dynamic_evaluation_before_html_import()
    {
        var catalog = HtmlElementCatalog.CreateDefault();
        var policy = new HtmlContentModelPolicy(catalog);
        var validator = CreateValidator(catalog);
        var renderer = new ScribanPageFragmentRenderer(
            new SecureScribanRenderer(),
            new HtmlFragmentImporter(
                catalog,
                new HtmlAttributePolicy(),
                policy,
                validator));

        var result = await renderer.RenderAsync(
            new PageRenderedFragment
            {
                NodeId = 73,
                Kind = PageRenderedFragmentKind.Scriban,
                Source = "{{ object.eval_template '<p>unsafe</p>' }}"
            },
            new PageFragmentRenderContext { SiteId = 42, Culture = "en-US" });

        await Assert.That(result).IsTypeOf<Result<HtmlPageContent>.Failure>();
    }

    private static (HtmlPageContent Content, PageCompositionDocument Composition, long ScopeId, long TemplateId, long BindingId)
        CreateListDocument()
    {
        var catalog = HtmlElementCatalog.CreateDefault();
        var scope = catalog.CreateElement("section");
        var template = catalog.CreateElement("article");
        var paragraph = catalog.CreateElement("p");
        paragraph.Children.Add(HtmlNode.CreateText("Placeholder"));
        template.Children.Add(paragraph);
        scope.Children.Add(template);
        var content = new HtmlPageContent();
        content.Root.Children.Add(scope);
        var composition = new PageCompositionDocument
        {
            ContentLists =
            [
                new PageContentListScope
                {
                    NodeId = scope.NodeId,
                    ContentTypeId = 501,
                    ContentTypeAlias = "articles",
                    TemplateRootNodeId = template.NodeId,
                    Query = new PageContentListQuery { PageSize = 2 },
                    EmptyState = PageContentEmptyStateBehavior.RenderNothing
                }
            ],
            FieldBindings =
            [
                new PageFieldBinding
                {
                    NodeId = paragraph.NodeId,
                    ScopeNodeId = scope.NodeId,
                    FieldName = "title",
                    Target = PageFieldBindingTarget.TextContent
                }
            ]
        };
        return (content, composition, scope.NodeId, template.NodeId, paragraph.NodeId);
    }

    private static PublishedContentItemProjection Projection(long id, string title) => new()
    {
        Id = id,
        ContentTypeAlias = "articles",
        Slug = $"item-{id}",
        Culture = "en-US",
        Fields = new Dictionary<string, JsonElement>
        {
            ["title"] = JsonSerializer.SerializeToElement(title)
        }
    };

    private static IHtmlContentValidator CreateValidator(HtmlElementCatalog? catalog = null)
    {
        catalog ??= HtmlElementCatalog.CreateDefault();
        var policy = new HtmlContentModelPolicy(catalog);
        return new HtmlContentValidator(catalog, policy, new HtmlAttributePolicy());
    }

    private static string FormatError(AeroError error) => error switch
    {
        AeroError.Validation validation => string.Join("; ", validation.Errors),
        _ => error.ToString() ?? error.GetType().Name
    };

    private static IEnumerable<HtmlNode> Descendants(HtmlNode node)
    {
        foreach (var child in node.Children)
        {
            yield return child;
            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }
}
