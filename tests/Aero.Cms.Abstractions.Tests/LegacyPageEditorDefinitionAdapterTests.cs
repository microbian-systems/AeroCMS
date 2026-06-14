using System.Text.Json;
using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Abstractions.Tests;

public sealed class LegacyPageEditorDefinitionAdapterTests
{
    [Test]
    public async Task Adapter_preserves_catalog_metadata_and_creates_default_node()
    {
        var adapter = new LegacyPageEditorDefinitionAdapter(new StubDefinition());

        var node = adapter.CreateDefaultNode();

        await Assert.That(adapter.CatalogId).IsEqualTo("test.hero");
        await Assert.That(adapter.Kind).IsEqualTo(NeoPageNodeKind.Block);
        await Assert.That(node.CatalogId).IsEqualTo("test.hero");
        await Assert.That(node.Kind).IsEqualTo(NeoPageNodeKind.Block);
        await Assert.That(node.Properties["title"].GetString()).IsEqualTo("Default title");
    }

    [Test]
    public async Task Legacy_canned_blocks_are_atomic_but_embeddable()
    {
        var adapter = new LegacyPageEditorDefinitionAdapter(new StubDefinition());

        await Assert.That(adapter.Composition.IsEmbeddable).IsTrue();
        await Assert.That(adapter.Composition.CanContainChildren).IsFalse();
        await Assert.That(adapter.Composition.MaximumChildren).IsEqualTo(0);
        await Assert.That(adapter.Composition.AllowedParentKinds)
            .Contains(NeoPageNodeKind.Container);
    }

    [Test]
    public async Task Explicit_capabilities_replace_legacy_defaults()
    {
        var expected = EditorCapabilitySet.Content | EditorCapabilitySet.Media;
        var adapter = new LegacyPageEditorDefinitionAdapter(new StubDefinition(), expected);

        await Assert.That(adapter.EditorCapabilities).IsEqualTo(expected);
    }

    private sealed class StubDefinition : IPageEditorBlockDefinition
    {
        public string CatalogId => "test.hero";
        public string DisplayName => "Test Hero";
        public string? Description => "Test";
        public string Category => "Test";
        public string Kind => "Block";
        public string IconName => "sparkles";
        public int SortOrder => 1;
        public bool PublicStaticSsrSafe => true;
        public Type? PreviewComponentType => null;
        public Type? PropertyEditorComponentType => null;

        public EditorBlock CreateDefaultEditorBlock() =>
            new() { Type = CatalogId, MainText = "Default title" };

        public NeoPageNode ToNeoPageNode(EditorBlock editorBlock) =>
            new()
            {
                CatalogId = editorBlock.Type,
                Kind = NeoPageNodeKind.Block,
                Properties = new Dictionary<string, JsonElement>
                {
                    ["title"] = JsonSerializer.SerializeToElement(editorBlock.MainText)
                }
            };

        public BlockBase? ToBlockBase(EditorBlock editorBlock) => null;
    }
}
