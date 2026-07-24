using Aero.Cms.Abstractions.Pages.Rendering;
using Aero.Cms.Modules.Pages.Rendering;
using Aero.Core.Railway;
using Shouldly;

namespace Aero.Cms.Core.Tests.Pages;

public sealed class PageRendererRegistryTests
{
    [Test]
    public void Missing_persisted_id_resolves_to_Aero_composition()
    {
        var aero = new StubPageRenderer(PageRendererIds.AeroComposition, "Aero");
        var registry = new PageRendererRegistry([aero]);

        var result = registry.Resolve(null);

        var success = result.ShouldBeOfType<Result<IPageRenderer>.Ok>();
        success.Value.ShouldBeSameAs(aero);
    }

    [Test]
    public void Unknown_renderer_fails_closed()
    {
        var registry = new PageRendererRegistry(
            [new StubPageRenderer(PageRendererIds.AeroComposition, "Aero")]);

        var result = registry.Resolve(PageRendererIds.SharpTs);

        result.ShouldBeOfType<Result<IPageRenderer>.Failure>();
    }

    [Test]
    public void Duplicate_renderer_ids_fail_registry_construction()
    {
        var renderers = new IPageRenderer[]
        {
            new StubPageRenderer(PageRendererIds.AeroComposition, "Aero"),
            new StubPageRenderer(PageRendererIds.AeroComposition, "Duplicate")
        };

        Should.Throw<InvalidOperationException>(() => new PageRendererRegistry(renderers))
            .Message.ShouldContain(PageRendererIds.AeroComposition);
    }

    [Test]
    public void Renderer_ids_require_a_lowercase_namespaced_value()
    {
        PageRendererIds.IsValid(PageRendererIds.AeroComposition).ShouldBeTrue();
        PageRendererIds.IsValid("plugin.custom-renderer").ShouldBeTrue();
        PageRendererIds.IsValid("SharpTS").ShouldBeFalse();
        PageRendererIds.IsValid("unqualified").ShouldBeFalse();
    }

    [Test]
    public void Scriban_renderer_is_resolved_and_advertised_as_a_source_renderer()
    {
        var scriban = new StubPageRenderer(
            PageRendererIds.Scriban,
            "Scriban",
            PageEditorKinds.Source,
            supportsFragments: true,
            isExperimental: false);
        var registry = new PageRendererRegistry(
            [new StubPageRenderer(PageRendererIds.AeroComposition, "Aero"), scriban]);

        registry.Resolve(PageRendererIds.Scriban)
            .ShouldBeOfType<Result<IPageRenderer>.Ok>()
            .Value.ShouldBeSameAs(scriban);
        registry.Descriptors.Single(descriptor =>
                descriptor.Id == PageRendererIds.Scriban)
            .ShouldBe(new PageRendererDescriptor(
                PageRendererIds.Scriban,
                "Scriban",
                PageEditorKinds.Source,
                SupportsFragments: true,
                IsExperimental: false));
    }

    private sealed class StubPageRenderer(
        string id,
        string displayName,
        string editorKind = PageEditorKinds.Source,
        bool supportsFragments = false,
        bool isExperimental = true) : IPageRenderer
    {
        public PageRendererId Id { get; } = new(id);

        public PageRendererDescriptor Descriptor { get; } = new(
            id,
            displayName,
            editorKind,
            SupportsFragments: supportsFragments,
            IsExperimental: isExperimental);

        public Task<Result<RenderedPage>> RenderAsync(
            PageRenderRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult<Result<RenderedPage>>(
                new RenderedPage(string.Empty, string.Empty, []));
    }
}
