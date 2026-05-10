using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Blocks.Editing;
using Aero.Cms.Abstractions.Blocks.Layout;
using Aero.Cms.Abstractions.Blocks.Serialization;
using Aero.Cms.Core.Extensions;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Shared.Blocks.Rendering;
using Aero.Cms.Web.Core.Blocks.Rendering;
using Aero.Core.Railway;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Radzen;
using TUnit.Core;

namespace Aero.Cms.BlockRendering.Tests;

public sealed class BlockRendererBaselineTests
{
    [Test]
    public async Task BlockRenderer_WithMarkdownBlock_RendersMarkdownContent()
    {
        var block = new MarkdownBlock
        {
            Content = "# Baseline Markdown"
        };

        var html = await RenderComponentAsync<BlockRenderer>(
            new Dictionary<string, object?>
            {
                ["Block"] = block
            });

        html.Should().Contain("markdown-block-content");
        html.Should().Contain("Baseline Markdown");
    }

    [Test]
    public async Task BlockRenderer_WithMarkdownBlock_EscapesInlineHtml()
    {
        var block = new MarkdownBlock
        {
            Content = """
# Safe Markdown
<script>alert('x')</script>
<strong>HTML stays literal</strong>
"""
        };

        var html = await RenderComponentAsync<BlockRenderer>(
            new Dictionary<string, object?>
            {
                ["Block"] = block
            });

        html.Should().Contain("Safe Markdown");
        html.Should().Contain("&lt;script&gt;");
        html.Should().Contain("&lt;strong&gt;HTML stays literal&lt;/strong&gt;");
        html.Should().NotContain("<script>");
        html.Should().NotContain("<strong>HTML stays literal</strong>");
    }

    [Test]
    public async Task BlockRenderer_WithNavigationContext_RendersOrderedNavigationLinks()
    {
        var block = new NavigationBlock
        {
            Title = "Main Navigation"
        };

        var navigation = new NavigationDetail(
            1,
            "main",
            "Main Navigation",
            [
                new NavigationItemDetail(2, "Second", "/second", null, 2, null),
                new NavigationItemDetail(1, "First", "/first", null, 1, null)
            ],
            DateTime.UtcNow,
            DateTime.UtcNow);

        var html = await RenderComponentAsync<BlockRenderer>(
            new Dictionary<string, object?>
            {
                ["Block"] = block,
                ["Navigation"] = navigation
            });

        html.Should().Contain("navigation-block");
        html.Should().Contain("Main Navigation");
        html.Should().Contain("href=\"/first\"");
        html.Should().Contain("href=\"/second\"");
        html.IndexOf("href=\"/first\"", StringComparison.Ordinal)
            .Should()
            .BeLessThan(html.IndexOf("href=\"/second\"", StringComparison.Ordinal));
    }

    [Test]
    public async Task BlockRenderer_WithUnknownBlock_RendersFallback()
    {
        var block = new UnknownBlock();

        var html = await RenderComponentAsync<BlockRenderer>(
            new Dictionary<string, object?>
            {
                ["Block"] = block
            });

        html.Should().Contain("Unknown block type: unknown_baseline");
        html.Should().Contain(nameof(UnknownBlock));
    }

    [Test]
    public async Task BlockRenderer_WithRawHtmlBlock_RendersRawMarkup()
    {
        var block = new RawHtmlBlock
        {
            Content = "<strong>Raw HTML baseline</strong>"
        };

        var html = await RenderComponentAsync<BlockRenderer>(
            new Dictionary<string, object?>
            {
                ["Block"] = block
            });

        html.Should().Contain("<strong>Raw HTML baseline</strong>");
        html.Should().NotContain("Unknown block type: raw_html");
    }

    [Test]
    public async Task BlockRenderer_WithRawHtmlBlock_SanitizesUnsafeMarkup()
    {
        var block = new RawHtmlBlock
        {
            Content = """<p onclick="alert('x')">Safe text</p><script>alert('x')</script><a href="javascript:alert('x')">bad link</a>"""
        };

        var html = await RenderComponentAsync<BlockRenderer>(
            new Dictionary<string, object?>
            {
                ["Block"] = block
            });

        html.Should().Contain("Safe text");
        var normalizedHtml = html.ToLowerInvariant();
        normalizedHtml.Should().NotContain("<script");
        normalizedHtml.Should().NotContain("onclick");
        normalizedHtml.Should().NotContain("javascript:");
    }

    [Test]
    public async Task RawHtmlRenderer_RendersRawMarkupDirectly()
    {
        var block = new RawHtmlBlock
        {
            Content = "<strong>Raw HTML baseline</strong>"
        };

        var html = await RenderComponentAsync<RawHtmlRenderer>(
            new Dictionary<string, object?>
            {
                ["Block"] = block
            });

        html.Should().Contain("<strong>Raw HTML baseline</strong>");
    }

    [Test]
    public void CmsBlockRenderRegistry_ResolvesCurrentSwitchSupportedBlocks()
    {
        string[] blockTypes =
        [
            "rich_text",
            "markdown",
            "heading",
            "image",
            "cta",
            "quote",
            "embed",
            "navigation",
            "aero_hero",
            "aero_features",
            "aero_cta",
            "aero_blog",
            "aero_pricing",
            "aero_teams",
            "aero_testimonials",
            "aero_faq",
            "aero_portfolio",
            "aero_contact",
            "aero_table",
            "aero_auth",
            "raw_html"
        ];

        foreach (var blockType in blockTypes)
        {
            CmsBlockRenderRegistry.TryGet(blockType, out var adapter)
                .Should()
                .BeTrue($"'{blockType}' should have a generated adapter");

            adapter.BlockType.Should().Be(blockType);
        }
    }

    [Test]
    public void CmsBlockManifest_ExposesRendererAndEditorMetadata()
    {
        CmsBlockManifest.TryGet("markdown", out var markdown)
            .Should()
            .BeTrue();

        markdown.DisplayName.Should().Be("Markdown Text");
        markdown.Category.Should().Be("Text");
        markdown.SchemaVersion.Should().Be(1);
        markdown.ModelType.Should().Be(typeof(MarkdownBlock));
        markdown.RendererType.Should().Be(typeof(MarkdownBlockRenderer));
        markdown.RendererParameterName.Should().Be("Block");

        CmsBlockManifest.TryGet("raw_html", out var rawHtml)
            .Should()
            .BeTrue();

        rawHtml.DisplayName.Should().Be("Raw HTML");
        rawHtml.Category.Should().Be("Advanced");
        rawHtml.ModelType.Should().Be(typeof(RawHtmlBlock));
        rawHtml.RendererType.Should().Be(typeof(RawHtmlRenderer));
    }

    [Test]
    public void CmsBlockManifestEditorMetadata_AdaptsManifestForEditorPalette()
    {
        var blockTypes = CmsBlockManifestEditorMetadata.GetAvailableBlockTypes();

        blockTypes.Should().ContainEquivalentOf(
            new BlockTypeInfo
            {
                Name = "markdown",
                DisplayName = "Markdown Text",
                Category = "Text",
                Type = typeof(MarkdownBlock)
            },
            options => options.Excluding(blockType => blockType.Description)
                .Excluding(blockType => blockType.Icon)
                .Excluding(blockType => blockType.SortOrder));

        CmsBlockManifestEditorMetadata.GetBlockTypeInfo("raw_html")
            .Should()
            .BeOfType<Option<BlockTypeInfo>.Some>()
            .Which.Value.DisplayName.Should().Be("Raw HTML");
    }

    [Test]
    public void GeneratedBlockModelManifest_ExposesAllDiscoveredBlockModelsForJsonAndMarten()
    {
        GeneratedBlockModelManifest.Blocks.Should().HaveCount(36);
        GeneratedBlockModelManifest.Blocks["markdown"].ModelType.Should().Be(typeof(MarkdownBlock));
        GeneratedBlockModelManifest.Blocks["markdown"].SchemaVersion.Should().Be(1);
        GeneratedBlockModelManifest.Blocks["youtube_player"].ModelType.Should().Be(typeof(YouTubeBlock));
        GeneratedBlockModelManifest.Blocks["columns"].ModelType.Should().Be(typeof(ColumnsBlock));

        GeneratedBlockJsonRegistration.ModelTypes.Should().Contain(typeof(MarkdownBlock));
        GeneratedBlockJsonRegistration.CollectionTypes.Should().Contain(typeof(List<MarkdownBlock>));
    }

    [Test]
    public async Task CmsBlockSliceRenderer_DelegatesLegacyVisitorPathToGeneratedBlazorRenderer()
    {
        var block = new MarkdownBlock
        {
            Content = "# Legacy Slice Bridge"
        };

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRadzenComponents();
        services.AddBlockSystemServices();
        services.AddSingleton<IJSRuntime, NoOpJSRuntime>();
        services.AddSingleton<IErrorBoundaryLogger, NoOpErrorBoundaryLogger>();

        await using var serviceProvider = services.BuildServiceProvider();
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

        await using var htmlRenderer = new HtmlRenderer(serviceProvider, loggerFactory);
        var blockHtmlRenderer = new CmsBlockHtmlRenderer(htmlRenderer);
        var registry = new BlockSliceRegistry();
        registry.Register(new CmsBlockSliceRenderer(blockHtmlRenderer));

        var html = RenderHtmlContent(registry.Visit(block));

        html.Should().Contain("markdown-block-content");
        html.Should().Contain("Legacy Slice Bridge");
    }

    private static async Task<string> RenderComponentAsync<TComponent>(
        IDictionary<string, object?> parameters)
        where TComponent : IComponent
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRadzenComponents();
        services.AddBlockSystemServices();
        services.AddSingleton<IJSRuntime, NoOpJSRuntime>();
        services.AddSingleton<IErrorBoundaryLogger, NoOpErrorBoundaryLogger>();

        await using var serviceProvider = services.BuildServiceProvider();
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

        await using var htmlRenderer = new HtmlRenderer(serviceProvider, loggerFactory);

        return await htmlRenderer.Dispatcher.InvokeAsync(async () =>
        {
            var parameterView = ParameterView.FromDictionary(parameters);
            var output = await htmlRenderer.RenderComponentAsync<TComponent>(parameterView);
            return output.ToHtmlString();
        });
    }

    private static string RenderHtmlContent(Microsoft.AspNetCore.Html.IHtmlContent content)
    {
        using var writer = new StringWriter();
        content.WriteTo(writer, System.Text.Encodings.Web.HtmlEncoder.Default);
        return writer.ToString();
    }

    private sealed class UnknownBlock : BlockBase
    {
        public override string BlockType => "unknown_baseline";

        public override Microsoft.AspNetCore.Html.IHtmlContent Accept(IBlockVisitor visitor)
            => visitor.Visit(this);
    }

    private sealed class NoOpJSRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => ValueTask.FromResult(default(TValue)!);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
            => ValueTask.FromResult(default(TValue)!);
    }

    private sealed class NoOpErrorBoundaryLogger : IErrorBoundaryLogger
    {
        public ValueTask LogErrorAsync(Exception exception)
            => ValueTask.CompletedTask;
    }
}
