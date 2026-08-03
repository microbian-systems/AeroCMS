using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Abstractions.Pages.Composition;
using Aero.Cms.Abstractions.Pages.Rendering;
using Aero.Cms.Core.Content.Templating;
using Aero.Cms.Html;
using Aero.Cms.Modules.Pages.Rendering;
using Aero.Core;
using Aero.Core.Railway;
using NSubstitute;
using Scriban.Runtime;
using Shouldly;

namespace Aero.Cms.Core.Tests.Pages;

public sealed class ScribanPageRendererTests
{
    [Test]
    public void Descriptor_advertises_the_stable_full_page_and_fragment_capability()
    {
        var renderer = CreateRenderer();

        renderer.Id.Value.ShouldBe(PageRendererIds.Scriban);
        renderer.Descriptor.DisplayName.ShouldBe("Scriban");
        renderer.Descriptor.EditorKind.ShouldBe(PageEditorKinds.Source);
        renderer.Descriptor.SupportsFragments.ShouldBeTrue();
        renderer.Descriptor.IsExperimental.ShouldBeFalse();
        renderer.Descriptor.SourceLanguage.ShouldBe("liquid");
        renderer.Descriptor.InitialSource.ShouldNotBeNull();
        renderer.Descriptor.InitialSource.ReplaceLineEndings("\n").ShouldBe(
            """
            <main class="aero-page">
              <h1>{{ page.title }}</h1>
            </main>
            """.ReplaceLineEndings("\n"));
        renderer.Descriptor.RequiresSource.ShouldBeTrue();
    }

    [Test]
    public async Task Preview_renders_only_closed_page_site_content_and_preview_scopes()
    {
        const long pageId = 9_007_199_254_740_993;
        const long siteId = 9_007_199_254_740_995;
        var source =
            """
            <section>
              <h1>{{ page.id }}|{{ page.title }}|{{ page.slug }}|{{ page.path }}|{{ page.culture }}</h1>
              <p>{{ site.id }}|{{ site.current_culture }}|{{ content.navigation.roots[0].id }}|{{ is_preview }}</p>
            </section>
            """;
        var request = Request(
            Source(0, source),
            isPreview: true,
            pageId: pageId,
            siteId: siteId,
            contentQueries: ContentResolution());

        var result = await CreateRenderer().RenderAsync(request);

        var success = result.ShouldBeOfType<Result<RenderedPage>.Ok>();
        success.Value.Markup.ShouldContain(
            "9007199254740993|Pure page|pure-page|/pure-page|en-US");
        success.Value.Markup.ShouldContain(
            "9007199254740995|en-US|9007199254740997|true");
        success.Value.ContentTypeAliases.ShouldBe(["category"]);
    }

    [Test]
    public async Task Unsaved_preview_exposes_a_null_page_identifier()
    {
        var result = await CreateRenderer().RenderAsync(
            Request(
                Source(0, "<p>{{ page.id == null }}</p>"),
                isPreview: true,
                pageId: null));

        result.ShouldBeOfType<Result<RenderedPage>.Ok>()
            .Value.Markup.ShouldBe("<p>true</p>");
    }

    [Test]
    [Arguments("{{ item.id }}")]
    [Arguments("{{ content_type.alias }}")]
    [Arguments("{{ fields.title }}")]
    public async Task Content_item_scopes_are_not_available_to_pure_pages(string source)
    {
        var result = await CreateRenderer().RenderAsync(
            Request(Source(101, source)));

        result.ShouldBeOfType<Result<RenderedPage>.Failure>();
    }

    [Test]
    public async Task Source_ownership_hash_and_preview_version_fail_closed()
    {
        var renderer = CreateRenderer();
        var validSource = Source(101, "<p>Valid</p>");
        var missing = await renderer.RenderAsync(Request(source: null));
        var wrongRenderer = await renderer.RenderAsync(Request(
            validSource with { RendererId = PageRendererIds.SharpTs }));
        var wrongHash = await renderer.RenderAsync(Request(
            validSource with { SourceHash = new string('0', 64) }));
        var unpersistedPublic = await renderer.RenderAsync(Request(
            Source(0, "<p>Preview only</p>"),
            isPreview: false));

        missing.ShouldBeOfType<Result<RenderedPage>.Failure>();
        wrongRenderer.ShouldBeOfType<Result<RenderedPage>.Failure>();
        wrongHash.ShouldBeOfType<Result<RenderedPage>.Failure>();
        unpersistedPublic.ShouldBeOfType<Result<RenderedPage>.Failure>();
    }

    [Test]
    public async Task Persisted_version_and_preview_hash_supply_stable_cache_identities()
    {
        var secureRenderer = new RecordingSecureScribanRenderer();
        var renderer = CreateRenderer(secureRenderer);
        var persisted = await renderer.RenderAsync(
            Request(Source(7_001, "<p>Persisted</p>")));
        var previewSource = Source(0, "<p>Preview</p>");
        var firstPreview = await renderer.RenderAsync(
            Request(previewSource, isPreview: true));
        var secondPreview = await renderer.RenderAsync(
            Request(previewSource, isPreview: true));

        persisted.ShouldBeOfType<Result<RenderedPage>.Ok>();
        firstPreview.ShouldBeOfType<Result<RenderedPage>.Ok>();
        secondPreview.ShouldBeOfType<Result<RenderedPage>.Ok>();
        secureRenderer.Definitions[0].Identity.ShouldBe(7_001);
        secureRenderer.Definitions[1].Identity.ShouldNotBe(0);
        secureRenderer.Definitions[2].Identity.ShouldBe(
            secureRenderer.Definitions[1].Identity);
    }

    private static ScribanPageRenderer CreateRenderer(
        ISecureScribanRenderer? scribanRenderer = null)
    {
        var catalog = HtmlElementCatalog.CreateDefault();
        var contentPolicy = new HtmlContentModelPolicy(catalog);
        var attributePolicy = new HtmlAttributePolicy();
        var validator = new HtmlContentValidator(
            catalog,
            contentPolicy,
            attributePolicy);
        var profileResolver = Substitute.For<ISiteStyleProfileResolver>();
        profileResolver.ResolveAsync(
                Arg.Any<long>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<IStyleProfile, AeroError>>(
                new Result<IStyleProfile, AeroError>.Ok(new NativeStyleProfile())));

        return new ScribanPageRenderer(
            scribanRenderer ?? new SecureScribanRenderer(),
            new HtmlFragmentImporter(
                catalog,
                attributePolicy,
                contentPolicy,
                validator),
            new HtmlStaticRenderer(
                catalog,
                contentPolicy,
                attributePolicy,
                validator),
            new NativeCssStyleCompiler(),
            profileResolver);
    }

    private static PageRenderRequest Request(
        PageRenderSource? source,
        bool isPreview = false,
        long? pageId = 101,
        long siteId = 201,
        PageContentQueryResolution? contentQueries = null)
        => new(
            new PageRenderMetadata(
                pageId,
                siteId,
                PageRendererIds.Scriban,
                "Pure page",
                "pure-page",
                "/pure-page",
                "en-US"),
            source,
            new HtmlPageContent(),
            null,
            ImmutableDictionary<long, int>.Empty,
            contentQueries ?? PageContentQueryResolution.Empty,
            isPreview);

    private static PageRenderSource Source(long versionId, string source)
        => new(
            versionId,
            PageRendererIds.Scriban,
            source,
            Hash(source));

    private static PageContentQueryResolution ContentResolution()
    {
        var node = new ContentNode(
            "9007199254740997",
            "category",
            "Root",
            "root",
            ImmutableDictionary<string, JsonElement>.Empty,
            []);
        var result = new ContentQueryResult(
            "navigation",
            "category",
            [node],
            1,
            false);
        return new PageContentQueryResolution
        {
            Results = ImmutableDictionary
                .Create<string, ContentQueryResult>(StringComparer.OrdinalIgnoreCase)
                .Add("navigation", result),
            ContentTypeAliases = ["category"]
        };
    }

    private static string Hash(string source)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)))
            .ToLowerInvariant();

    private sealed class RecordingSecureScribanRenderer : ISecureScribanRenderer
    {
        public List<ScribanRenderDefinition> Definitions { get; } = [];

        public Task<Result<string, AeroError>> RenderAsync(
            ScribanRenderDefinition definition,
            ScribanContentRenderModel model,
            CancellationToken cancellationToken = default,
            IReadOnlyDictionary<string, ScriptObject>? imports = null)
            => throw new NotSupportedException();

        public Task<Result<string>> RenderTrustedAsync(
            ScribanRenderDefinition definition,
            ScriptObject trustedGlobals,
            CancellationToken cancellationToken = default)
        {
            Definitions.Add(definition);
            return Task.FromResult<Result<string>>(
                new Result<string>.Ok("<p>Rendered</p>"));
        }
    }
}
