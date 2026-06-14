using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Composition;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Abstractions.Tests;

public sealed class CompositionTreeEditorTests
{
    [Test]
    public async Task Drop_inserts_a_new_node_into_a_container()
    {
        var roots = new List<NeoPageNode> { Container("container") };
        var editor = CreateEditor();

        var result = editor.Drop(
            roots,
            new CompositionDropRequest(
                Text("text"),
                "container",
                NeoDropZoneDefinition.DefaultId,
                0));

        var updated = ((Result<IReadOnlyList<NeoPageNode>, AeroError>.Ok)result).Value;
        await Assert.That(updated[0].Children).Count().IsEqualTo(1);
        await Assert.That(updated[0].Children[0].NodeId).IsEqualTo("text");
        await Assert.That(roots[0].Children).IsEmpty();
    }

    [Test]
    public async Task Drop_reorders_a_node_without_exceeding_cardinality()
    {
        var container = Container("container", maximumChildren: 2);
        container.Children = [Text("one"), Text("two")];
        var editor = CreateEditor(maximumChildren: 2);

        var result = editor.Drop(
            [container],
            new CompositionDropRequest(
                container.Children[0],
                "container",
                NeoDropZoneDefinition.DefaultId,
                1));

        var updated = ((Result<IReadOnlyList<NeoPageNode>, AeroError>.Ok)result).Value;
        await Assert.That(updated[0].Children.Select(node => node.NodeId))
            .IsEquivalentTo(["two", "one"]);
    }

    [Test]
    public async Task Drop_rejects_a_cycle_without_mutating_the_tree()
    {
        var parent = Container("parent");
        var child = Container("child");
        parent.Children.Add(child);
        var editor = CreateEditor();

        var result = editor.Drop(
            [parent],
            new CompositionDropRequest(
                parent,
                "child",
                NeoDropZoneDefinition.DefaultId,
                0));

        await Assert.That(result).IsTypeOf<Result<IReadOnlyList<NeoPageNode>, AeroError>.Failure>();
        await Assert.That(parent.Children[0].NodeId).IsEqualTo("child");
    }

    [Test]
    public async Task Remove_returns_a_new_tree_without_the_node()
    {
        var container = Container("container");
        container.Children.Add(Text("text"));
        var editor = CreateEditor();

        var result = editor.Remove([container], "text");

        var updated = ((Result<IReadOnlyList<NeoPageNode>, AeroError>.Ok)result).Value;
        await Assert.That(updated[0].Children).IsEmpty();
        await Assert.That(container.Children).Count().IsEqualTo(1);
    }

    [Test]
    public async Task Drop_moves_a_node_between_sibling_containers()
    {
        var source = Container("source");
        source.Children.Add(Text("text"));
        var target = Container("target");
        var root = Container("root");
        root.Children = [source, target];
        var editor = CreateEditor();

        var result = editor.Drop(
            [root],
            new CompositionDropRequest(
                source.Children[0],
                "target",
                NeoDropZoneDefinition.DefaultId,
                0));

        var updated = ((Result<IReadOnlyList<NeoPageNode>, AeroError>.Ok)result).Value[0];
        await Assert.That(updated.Children[0].Children).IsEmpty();
        await Assert.That(updated.Children[1].Children).Count().IsEqualTo(1);
        await Assert.That(updated.Children[1].Children[0].NodeId).IsEqualTo("text");
    }

    [Test]
    public async Task Rejected_cross_container_move_leaves_original_tree_unchanged()
    {
        var source = Container("source");
        source.Children.Add(Text("text"));
        var fullTarget = Container("target");
        fullTarget.Children.Add(Text("existing"));
        var root = Container("root");
        root.Children = [source, fullTarget];
        var editor = CreateEditor(maximumChildren: 1);

        var result = editor.Drop(
            [root],
            new CompositionDropRequest(
                source.Children[0],
                "target",
                NeoDropZoneDefinition.DefaultId,
                1));

        await Assert.That(result)
            .IsTypeOf<Result<IReadOnlyList<NeoPageNode>, AeroError>.Failure>();
        await Assert.That(source.Children).Count().IsEqualTo(1);
        await Assert.That(fullTarget.Children).Count().IsEqualTo(1);
    }

    private static CompositionTreeEditor CreateEditor(int? maximumChildren = null)
    {
        var capabilities = new Dictionary<string, ICompositionCapabilities>
        {
            ["primitive.text"] = CompositionCapabilities.Leaf(
                NeoPageNodeKind.Container,
                NeoPageNodeKind.Component,
                NeoPageNodeKind.Section),
            ["primitive.container"] = CompositionCapabilities.Container(
                [NeoPageNodeKind.Primitive, NeoPageNodeKind.Container, NeoPageNodeKind.Component],
                [NeoPageNodeKind.Container, NeoPageNodeKind.Component, NeoPageNodeKind.Section],
                maximumChildren)
        };

        return new CompositionTreeEditor(
            new CompositionPolicy(new Resolver(capabilities)));
    }

    private static NeoPageNode Text(string id) =>
        new()
        {
            NodeId = id,
            CatalogId = "primitive.text",
            Kind = NeoPageNodeKind.Primitive
        };

    private static NeoPageNode Container(string id, int? maximumChildren = null) =>
        new()
        {
            NodeId = id,
            CatalogId = "primitive.container",
            Kind = NeoPageNodeKind.Container
        };

    private sealed class Resolver(
        IReadOnlyDictionary<string, ICompositionCapabilities> capabilities)
        : ICompositionCapabilityResolver
    {
        public bool TryGet(string catalogId, out ICompositionCapabilities value) =>
            capabilities.TryGetValue(catalogId, out value!);
    }
}
