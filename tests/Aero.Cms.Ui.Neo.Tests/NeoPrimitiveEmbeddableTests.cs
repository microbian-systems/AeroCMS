using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Embed;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Composition;
using Aero.Cms.Shared.Pages.Manager.PageEditor.Services;
using Aero.Cms.Ui.Neo;
using Aero.Cms.Ui.Neo.Embed;
using FluentAssertions;
using TUnit.Core;

namespace Aero.Cms.Ui.Neo.Tests;

public sealed class NeoPrimitiveEmbeddableTests
{
    private static readonly string[] ExpectedCatalogIds =
    [
        "primitive.container",
        "primitive.text",
        "primitive.heading",
        "primitive.button",
        "primitive.image",
        "primitive.pill",
        "primitive.icon",
        "primitive.separator",
        "primitive.code",
        "primitive.section",
        "primitive.article",
        "primitive.header",
        "primitive.footer",
        "primitive.aside",
        "primitive.nav",
        "preset.card",
        "primitive.grid",
        "primitive.grid-row",
        "primitive.grid-cell",
        "primitive.blockquote",
        "primitive.form",
        "primitive.embed"
    ];

    [Test]
    public void AllNeoPrimitiveDescriptors_ImplementIEmbeddable_AndAreEmbeddable()
    {
        var provider = new NeoPageEditorBlockProvider();
        var descriptors = provider.GetEditorDefinitions();

        var actualCatalogIds = descriptors.Select(d => d.CatalogId).OrderBy(id => id).ToArray();
        var expectedCatalogIds = ExpectedCatalogIds.OrderBy(id => id).ToArray();
        actualCatalogIds.Should().Contain(expectedCatalogIds,
            "because every expected primitive descriptor should be present");

        foreach (var descriptor in descriptors)
        {
            descriptor.Catalog.Should().BeAssignableTo<IEmbeddable>(
                $"because primitive '{descriptor.CatalogId}' ({descriptor.Catalog.DisplayName}) should be embeddable");

            descriptor.Catalog.Composition.IsEmbeddable.Should().BeTrue(
                $"because primitive '{descriptor.CatalogId}' should have IsEmbeddable == true");
        }
    }

    [Test]
    public void GridCellDescriptor_IsNotDraggable()
    {
        var provider = new NeoPageEditorBlockProvider();
        var cellDescriptor = provider.GetEditorDefinitions()
            .Single(d => d.CatalogId == "primitive.grid-cell");

        ((IEditorInteractionProvider)cellDescriptor.Catalog).Interaction
            .HasFlag(EditorInteractionCapabilities.Draggable)
            .Should().BeFalse(
                "because GridCell is fixed inside a GridRow — users move content, not the cell itself");
    }

    [Test]
    public void ContainerDescriptors_SupportPasteTarget()
    {
        var provider = new NeoPageEditorBlockProvider();
        var containerDescriptors = provider.GetEditorDefinitions()
            .Where(d => d.Catalog.Composition.CanContainChildren
                        && d.Catalog is IEditorInteractionProvider);

        foreach (var descriptor in containerDescriptors)
        {
            ((IEditorInteractionProvider)descriptor.Catalog).Interaction
                .HasFlag(EditorInteractionCapabilities.PasteTarget)
                .Should().BeTrue(
                    $"because container '{descriptor.CatalogId}' ({descriptor.Catalog.DisplayName}) should accept dropped/pasted children");
        }
    }

    [Test]
    public void EmbedResolverPipeline_ResolvesProviderUrls()
    {
        // Arrange: build the pipeline with all resolvers in order
        var resolvers = new IEmbedUrlResolver[]
        {
            new YouTubeEmbedResolver(),
            new VimeoEmbedResolver(),
            new GoogleMapsEmbedResolver(),
            new CalendlyEmbedResolver(),
            new GenericIframeResolver()
        };
        var pipeline = new EmbedResolverPipeline(resolvers);
        var allowList = new EmbedAllowList();

        // YouTube
        var ytResult = pipeline.Resolve(new Uri("https://www.youtube.com/watch?v=dQw4w9WgXcQ"));
        ytResult.Should().NotBeNull();
        ytResult!.EmbedSrc.Should().Contain("youtube-nocookie.com/embed/");
        ytResult.DefaultRatio.Should().Be(AspectRatio.Widescreen);
        ytResult.DefaultSandbox.Should().Be(SandboxFlags.Video);

        // YouTube short link
        var ytShort = pipeline.Resolve(new Uri("https://youtu.be/dQw4w9WgXcQ"));
        ytShort.Should().NotBeNull();
        ytShort!.EmbedSrc.Should().Contain("youtube-nocookie.com/embed/");

        // Vimeo
        var vmResult = pipeline.Resolve(new Uri("https://vimeo.com/123456789"));
        vmResult.Should().NotBeNull();
        vmResult!.EmbedSrc.Should().Contain("player.vimeo.com/video/");

        // Generic HTTPS fallback
        var genericResult = pipeline.Resolve(new Uri("https://example.com/widget"));
        genericResult.Should().NotBeNull();
        genericResult!.EmbedSrc.Should().Be("https://example.com/widget");
        genericResult.DefaultSandbox.Should().Be(SandboxFlags.Strict);

        // Unknown protocol returns null
        var ftpResult = pipeline.Resolve(new Uri("ftp://example.com/file"));
        ftpResult.Should().BeNull();

        // AllowList: youtube-nocookie.com IS allowed
        var safeResult = pipeline.ResolveSafe(new Uri("https://www.youtube.com/watch?v=dQw4w9WgXcQ"), allowList);
        safeResult.Should().NotBeNull();

        // AllowList: unknown-host.com is NOT allowed
        var blockedResult = pipeline.ResolveSafe(new Uri("https://unknown-host.com/widget"), allowList);
        blockedResult.Should().BeNull();
    }

    [Test]
    public void CompositionPolicy_CatalogIdEnforcement_RejectsGridRowInSection()
    {
        // Arrange: resolve capabilities from the provider
        var provider = new NeoPageEditorBlockProvider();
        var resolver = new DescriptorCompositionCapabilityResolver(provider);
        var policy = new CompositionPolicy(resolver);

        var section = new NeoPageNode
        {
            NodeId = "test-section",
            CatalogId = "primitive.section",
            Kind = NeoPageNodeKind.Section,
            Children = []
        };

        var gridRow = new NeoPageNode
        {
            NodeId = "test-row",
            CatalogId = "primitive.grid-row",
            Kind = NeoPageNodeKind.Container,
            Children = []
        };

        var context = new CompositionTreeContext(
            ExistingChildrenInDropZone: 0,
            MovingNodeAlreadyInTargetDropZone: false,
            MovingNodeDescendantIds: new HashSet<string>());

        // Act: try to place GridRow inside Section
        var result = policy.ValidatePlacement(gridRow, section, "default", context);

        // Assert: should reject because GridRow's AllowedParentCatalogIds = { "primitive.grid" }
        result.IsSuccess.Should().BeFalse(
            "GridRow should not be placed directly inside a Section — AllowedParentCatalogIds restricts to 'primitive.grid'");
    }

    [Test]
    public void CompositionPolicy_CatalogIdEnforcement_RejectsGridCellInSection()
    {
        var provider = new NeoPageEditorBlockProvider();
        var resolver = new DescriptorCompositionCapabilityResolver(provider);
        var policy = new CompositionPolicy(resolver);

        var section = new NeoPageNode
        {
            NodeId = "test-section",
            CatalogId = "primitive.section",
            Kind = NeoPageNodeKind.Section
        };

        var gridCell = new NeoPageNode
        {
            NodeId = "test-cell",
            CatalogId = "primitive.grid-cell",
            Kind = NeoPageNodeKind.Container
        };

        var context = new CompositionTreeContext(
            ExistingChildrenInDropZone: 0,
            MovingNodeAlreadyInTargetDropZone: false,
            MovingNodeDescendantIds: new HashSet<string>());

        var result = policy.ValidatePlacement(gridCell, section, "default", context);

        result.IsSuccess.Should().BeFalse(
            "GridCell should not be placed directly inside a Section — AllowedParentCatalogIds restricts to 'primitive.grid-row'");
    }

    [Test]
    public void CompositionPolicy_AllowsGridRowInsideGrid()
    {
        var provider = new NeoPageEditorBlockProvider();
        var resolver = new DescriptorCompositionCapabilityResolver(provider);
        var policy = new CompositionPolicy(resolver);

        var grid = new NeoPageNode
        {
            NodeId = "test-grid",
            CatalogId = "primitive.grid",
            Kind = NeoPageNodeKind.Container
        };

        var gridRow = new NeoPageNode
        {
            NodeId = "test-row",
            CatalogId = "primitive.grid-row",
            Kind = NeoPageNodeKind.Container
        };

        var context = new CompositionTreeContext(
            ExistingChildrenInDropZone: 0,
            MovingNodeAlreadyInTargetDropZone: false,
            MovingNodeDescendantIds: new HashSet<string>());

        var result = policy.ValidatePlacement(gridRow, grid, "grid-rows", context);

        result.IsSuccess.Should().BeTrue(
            "GridRow should be allowed inside Grid via the 'grid-rows' drop zone");
    }

    [Test]
    public void LeafPrimitives_DoNotSupportPasteTarget()
    {
        var provider = new NeoPageEditorBlockProvider();
        var leafDescriptors = provider.GetEditorDefinitions()
            .Where(d => !d.Catalog.Composition.CanContainChildren
                        && d.Catalog is IEditorInteractionProvider);

        leafDescriptors.Should().NotBeEmpty();

        foreach (var descriptor in leafDescriptors)
        {
            ((IEditorInteractionProvider)descriptor.Catalog).Interaction
                .HasFlag(EditorInteractionCapabilities.PasteTarget)
                .Should().BeFalse(
                    $"because leaf primitive '{descriptor.CatalogId}' should not accept pasted children");
        }
    }

    [Test]
    public void EditorNodeActionProvider_MapsCapabilitiesToActions()
    {
        var provider = new EditorNodeActionProvider();

        // Container: full capabilities with clipboard and position
        var containerCaps = EditorInteractionCapabilities.Selectable
            | EditorInteractionCapabilities.Editable
            | EditorInteractionCapabilities.Draggable
            | EditorInteractionCapabilities.Duplicatable
            | EditorInteractionCapabilities.Deletable
            | EditorInteractionCapabilities.Copyable
            | EditorInteractionCapabilities.PasteTarget;
        var containerCtx = new EditorNodeActionContext(
            HasClipboardContent: true, CanMoveUp: true, CanMoveDown: true, CanSaveAsCustom: false);

        var containerActions = provider.GetAvailableActions(containerCaps, containerCtx);
        containerActions.Should().Contain(EditorNodeAction.Edit);
        containerActions.Should().Contain(EditorNodeAction.Duplicate);
        containerActions.Should().Contain(EditorNodeAction.Delete);
        containerActions.Should().Contain(EditorNodeAction.Copy);
        containerActions.Should().Contain(EditorNodeAction.Paste);
        containerActions.Should().Contain(EditorNodeAction.MoveUp);
        containerActions.Should().Contain(EditorNodeAction.MoveDown);
        containerActions.Should().NotContain(EditorNodeAction.SaveAsCustom);
        containerActions.Should().NotContain(EditorNodeAction.MediaSelect);

        // Leaf with media: should have MediaSelect
        var mediaCaps = EditorInteractionCapabilities.Selectable
            | EditorInteractionCapabilities.Editable
            | EditorInteractionCapabilities.Draggable
            | EditorInteractionCapabilities.Copyable
            | EditorInteractionCapabilities.MediaSelectable;
        var mediaCtx = new EditorNodeActionContext(
            HasClipboardContent: false, CanMoveUp: false, CanMoveDown: false, CanSaveAsCustom: false);

        var mediaActions = provider.GetAvailableActions(mediaCaps, mediaCtx);
        mediaActions.Should().Contain(EditorNodeAction.MediaSelect);
        mediaActions.Should().Contain(EditorNodeAction.Edit);
        mediaActions.Should().NotContain(EditorNodeAction.Paste); // no PasteTarget
        mediaActions.Should().NotContain(EditorNodeAction.Duplicate);

        // Paste should NOT appear when clipboard is empty
        var emptyCtx = new EditorNodeActionContext(
            HasClipboardContent: false, CanMoveUp: false, CanMoveDown: false, CanSaveAsCustom: false);
        var pasteActions = provider.GetAvailableActions(containerCaps, emptyCtx);
        pasteActions.Should().NotContain(EditorNodeAction.Paste);

        // SaveAsCustom should appear when context allows
        var customCtx = new EditorNodeActionContext(
            HasClipboardContent: false, CanMoveUp: false, CanMoveDown: false, CanSaveAsCustom: true);
        var customActions = provider.GetAvailableActions(EditorInteractionCapabilities.Editable, customCtx);
        customActions.Should().Contain(EditorNodeAction.SaveAsCustom);
    }

    /// <summary>
    /// Simple in-memory resolver that looks up composition capabilities
    /// from the provider's editor definitions.
    /// </summary>
    private sealed class DescriptorCompositionCapabilityResolver(NeoPageEditorBlockProvider provider)
        : ICompositionCapabilityResolver
    {
        private readonly Dictionary<string, ICompositionCapabilities> _capabilities =
            provider.GetEditorDefinitions()
                .ToDictionary(d => d.CatalogId, d => d.Catalog.Composition);

        public bool TryGet(string catalogId, out ICompositionCapabilities capabilities) =>
            _capabilities.TryGetValue(catalogId, out capabilities!);
    }

    [Test]
    public void AllDescriptorFactories_CreateDefaultNode_WithValidIdentity()
    {
        var provider = new NeoPageEditorBlockProvider();
        var descriptors = provider.GetEditorDefinitions();

        descriptors.Should().NotBeEmpty();
        var definitionsWithoutFactories = new List<string>();

        foreach (var descriptor in descriptors)
        {
            var node = descriptor.NodeFactory.CreateDefaultNode();

            node.Should().NotBeNull($"descriptor '{descriptor.CatalogId}' should produce a node");
            node.NodeId.Should().NotBeNullOrWhiteSpace(
                $"descriptor '{descriptor.CatalogId}' node should have a NodeId");
            node.CatalogId.Should().Be(descriptor.CatalogId,
                $"descriptor '{descriptor.CatalogId}' node should match its catalog ID");
            node.Kind.Should().Be(descriptor.Catalog.Kind,
                $"descriptor '{descriptor.CatalogId}' node Kind should match definition");

            // Leaf nodes should have no children
            if (!descriptor.Catalog.Composition.CanContainChildren)
            {
                node.Children.Should().BeEmpty(
                    $"leaf descriptor '{descriptor.CatalogId}' should produce a node with no children");
            }
        }
    }

    [Test]
    public void ContainerDescriptors_HaveDropZones()
    {
        var provider = new NeoPageEditorBlockProvider();
        var containerDescriptors = provider.GetEditorDefinitions()
            .Where(d => d.Catalog.Composition.CanContainChildren);

        containerDescriptors.Should().NotBeEmpty();

        foreach (var descriptor in containerDescriptors)
        {
            descriptor.Catalog.Composition.SupportedDropZones.Should().NotBeEmpty(
                $"container '{descriptor.CatalogId}' should declare at least one drop zone");
        }
    }
}
