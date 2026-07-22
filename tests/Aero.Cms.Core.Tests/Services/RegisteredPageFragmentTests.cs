using System.Text.Json;
using Aero.Cms.Abstractions.Content.Composition;
using Aero.Cms.Abstractions.Pages.Composition;
using Aero.Cms.Html;
using Aero.Cms.Modules.Pages.Rendering;
using Aero.Core;
using Aero.Core.Railway;
using NSubstitute;
using Shouldly;

namespace Aero.Cms.Core.Tests.Services;

public sealed class RegisteredPageFragmentTests
{
    [Test]
    public async Task Composition_snapshot_and_json_preserve_independent_typed_parameters()
    {
        var values = new Dictionary<string, JsonElement>
        {
            ["message"] = JsonSerializer.SerializeToElement("Original"),
            ["dismissible"] = JsonSerializer.SerializeToElement(true)
        };
        var composition = new PageCompositionDocument
        {
            RegisteredFragments =
            [
                new PageRegisteredFragment
                {
                    NodeId = 123,
                    Key = "core.site-notice",
                    Parameters = values
                }
            ]
        };

        var snapshot = composition.CreateSnapshot();
        values["message"] = JsonSerializer.SerializeToElement("Changed");
        var json = JsonSerializer.Serialize(
            snapshot,
            PageCompositionJsonContext.Default.PageCompositionDocument);
        var roundTrip = JsonSerializer.Deserialize(
            json,
            PageCompositionJsonContext.Default.PageCompositionDocument)!;

        snapshot.RegisteredFragments.ShouldNotBeSameAs(composition.RegisteredFragments);
        snapshot.RegisteredFragments.Single().Parameters["message"].GetString().ShouldBe("Original");
        roundTrip.RegisteredFragments.Single().Parameters["dismissible"].GetBoolean().ShouldBeTrue();
        await Task.CompletedTask;
    }

    [Test]
    public async Task Registry_rejects_duplicate_normalized_and_invalid_keys()
    {
        var importer = CreateImporter();
        Should.Throw<InvalidOperationException>(() => new PageRegisteredFragmentRegistry(
            [new StubProvider("Core.Notice"), new StubProvider("core.notice")],
            importer));
        Should.Throw<InvalidOperationException>(() => new PageRegisteredFragmentRegistry(
            [new StubProvider("unsafe/view")],
            importer));
        await Task.CompletedTask;
    }

    [Test]
    public async Task Registry_applies_defaults_and_rejects_schema_mismatches()
    {
        var registry = CreateRegistry(new SiteNoticePageRegisteredFragmentProvider());
        var valid = registry.Validate(new PageRegisteredFragment
        {
            Key = "CORE.SITE-NOTICE",
            Parameters = new Dictionary<string, JsonElement>
            {
                ["message"] = JsonSerializer.SerializeToElement("Deployment complete")
            }
        });

        valid.ShouldBeOfType<Result<PageRegisteredFragment>.Ok>();
        var normalized = ((Result<PageRegisteredFragment>.Ok)valid).Value;
        normalized.Key.ShouldBe("core.site-notice");
        normalized.Parameters["tone"].GetString().ShouldBe("info");
        normalized.Parameters["dismissible"].GetBoolean().ShouldBeFalse();

        registry.Validate(new PageRegisteredFragment
        {
            Key = "core.site-notice",
            Parameters = new Dictionary<string, JsonElement>
            {
                ["message"] = JsonSerializer.SerializeToElement(42),
                ["unknown"] = JsonSerializer.SerializeToElement("value")
            }
        }).ShouldBeOfType<Result<PageRegisteredFragment>.Failure>();
        await Task.CompletedTask;
    }

    [Test]
    public async Task Expansion_renders_registered_provider_without_mutating_saved_tree()
    {
        var catalog = HtmlElementCatalog.CreateDefault();
        var target = catalog.CreateElement("section");
        target.Children.Add(HtmlNode.CreateText("Editor placeholder"));
        var content = new HtmlPageContent();
        content.Root.Children.Add(target);
        var composition = new PageCompositionDocument
        {
            RegisteredFragments =
            [
                new PageRegisteredFragment
                {
                    NodeId = target.NodeId,
                    Key = "core.site-notice",
                    Parameters = new Dictionary<string, JsonElement>
                    {
                        ["message"] = JsonSerializer.SerializeToElement("Hello")
                    }
                }
            ]
        };
        var validator = CreateValidator(catalog);
        var expander = new PageCompositionExpander(
            Substitute.For<IContentCompositionResolver>(),
            validator,
            [],
            CreateRegistry(new SiteNoticePageRegisteredFragmentProvider(), catalog));

        var result = await expander.ExpandAsync(42, "en-US", content, composition);

        result.ShouldBeOfType<Result<PageCompositionExpansion, AeroError>.Ok>();
        var expanded = ((Result<PageCompositionExpansion, AeroError>.Ok)result).Value.Content;
        HtmlTreeOperations.FindById(expanded.Root, target.NodeId)!.Children.Single().TagName.ShouldBe("aside");
        target.Children.Single().Text.ShouldBe("Editor placeholder");
    }

    [Test]
    public async Task Expansion_fails_closed_for_missing_or_invalid_provider_output()
    {
        var catalog = HtmlElementCatalog.CreateDefault();
        var target = catalog.CreateElement("section");
        var content = new HtmlPageContent();
        content.Root.Children.Add(target);
        PageCompositionDocument Composition(string key) => new()
        {
            RegisteredFragments = [new PageRegisteredFragment { NodeId = target.NodeId, Key = key }]
        };
        var validator = CreateValidator(catalog);

        var missing = await new PageCompositionExpander(
            Substitute.For<IContentCompositionResolver>(), validator)
            .ExpandAsync(42, "en-US", content, Composition("core.missing"));
        missing.ShouldBeOfType<Result<PageCompositionExpansion, AeroError>.Failure>();

        var invalid = await new PageCompositionExpander(
            Substitute.For<IContentCompositionResolver>(),
            validator,
            [],
            CreateRegistry(new UnsafeProvider(), catalog))
            .ExpandAsync(42, "en-US", content, Composition("core.unsafe"));
        invalid.ShouldBeOfType<Result<PageCompositionExpansion, AeroError>.Failure>();
    }

    private static PageRegisteredFragmentRegistry CreateRegistry(
        IPageRegisteredFragmentProvider provider,
        HtmlElementCatalog? catalog = null)
        => new([provider], CreateImporter(catalog));

    private static IHtmlFragmentImporter CreateImporter(HtmlElementCatalog? catalog = null)
    {
        catalog ??= HtmlElementCatalog.CreateDefault();
        var policy = new HtmlContentModelPolicy(catalog);
        return new HtmlFragmentImporter(
            catalog,
            new HtmlAttributePolicy(),
            policy,
            CreateValidator(catalog));
    }

    private static IHtmlContentValidator CreateValidator(HtmlElementCatalog catalog)
        => new HtmlContentValidator(catalog, new HtmlContentModelPolicy(catalog), new HtmlAttributePolicy());

    private sealed class StubProvider(string key) : IPageRegisteredFragmentProvider
    {
        public PageRegisteredFragmentDescriptor Descriptor { get; } = new()
        {
            Key = key,
            DisplayName = key
        };

        public Task<Result<string>> RenderAsync(
            PageRegisteredFragment fragment,
            PageFragmentRenderContext context,
            CancellationToken cancellationToken = default)
            => Task.FromResult<Result<string>>("<p>Safe</p>");
    }

    private sealed class UnsafeProvider : IPageRegisteredFragmentProvider
    {
        public PageRegisteredFragmentDescriptor Descriptor { get; } = new()
        {
            Key = "core.unsafe",
            DisplayName = "Unsafe"
        };

        public Task<Result<string>> RenderAsync(
            PageRegisteredFragment fragment,
            PageFragmentRenderContext context,
            CancellationToken cancellationToken = default)
            => Task.FromResult<Result<string>>("<script>alert('unsafe')</script>");
    }
}
