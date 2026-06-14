using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Composition;
using TUnit.Core;

namespace Aero.Cms.Abstractions.Tests;

public sealed class CompositionPolicyTests
{
    private static readonly IReadOnlySet<NeoPageNodeKind> ContainerParents =
        new HashSet<NeoPageNodeKind>
        {
            NeoPageNodeKind.Container,
            NeoPageNodeKind.Section,
            NeoPageNodeKind.Component
        };

    [Test]
    public async Task ValidatePlacement_AllowsPrimitiveInContainer()
    {
        var policy = CreatePolicy();

        var result = policy.ValidatePlacement(
            Node("ui.button", NeoPageNodeKind.Primitive, "button"),
            Node("ui.container", NeoPageNodeKind.Container, "container"),
            NeoDropZoneDefinition.DefaultId,
            CompositionTreeContext.Empty);

        await Assert.That(result.IsSuccess).IsTrue();
    }

    [Test]
    public async Task ValidatePlacement_RejectsChildInLeaf()
    {
        var policy = CreatePolicy();

        var result = policy.ValidatePlacement(
            Node("ui.text", NeoPageNodeKind.Primitive, "text"),
            Node("ui.button", NeoPageNodeKind.Primitive, "button"),
            NeoDropZoneDefinition.DefaultId,
            CompositionTreeContext.Empty);

        await Assert.That(result.IsFailure).IsTrue();
    }

    [Test]
    public async Task ValidatePlacement_RejectsCycle()
    {
        var policy = CreatePolicy();
        var context = new CompositionTreeContext(
            new HashSet<string>(StringComparer.Ordinal) { "descendant" },
            0);

        var result = policy.ValidatePlacement(
            Node("ui.container", NeoPageNodeKind.Container, "moving"),
            Node("ui.container", NeoPageNodeKind.Container, "descendant"),
            NeoDropZoneDefinition.DefaultId,
            context);

        await Assert.That(result.IsFailure).IsTrue();
    }

    [Test]
    public async Task ValidatePlacement_RejectsFullDropZone()
    {
        var policy = CreatePolicy(maximumChildren: 1);

        var result = policy.ValidatePlacement(
            Node("ui.image", NeoPageNodeKind.Primitive, "image"),
            Node("ui.container", NeoPageNodeKind.Container, "container"),
            NeoDropZoneDefinition.DefaultId,
            new CompositionTreeContext(new HashSet<string>(), 1));

        await Assert.That(result.IsFailure).IsTrue();
    }

    [Test]
    public async Task ValidatePlacement_RejectsUnknownDropZone()
    {
        var policy = CreatePolicy();

        var result = policy.ValidatePlacement(
            Node("ui.text", NeoPageNodeKind.Primitive, "text"),
            Node("ui.container", NeoPageNodeKind.Container, "container"),
            "missing",
            CompositionTreeContext.Empty);

        await Assert.That(result.IsFailure).IsTrue();
    }

    [Test]
    public async Task ValidatePlacement_AllowsReorderWithinFullDropZone()
    {
        var policy = CreatePolicy(maximumChildren: 1);

        var result = policy.ValidatePlacement(
            Node("ui.image", NeoPageNodeKind.Primitive, "image"),
            Node("ui.container", NeoPageNodeKind.Container, "container"),
            NeoDropZoneDefinition.DefaultId,
            new CompositionTreeContext(
                new HashSet<string>(),
                ExistingChildrenInDropZone: 1,
                MovingNodeAlreadyInTargetDropZone: true));

        await Assert.That(result.IsSuccess).IsTrue();
    }

    private static CompositionPolicy CreatePolicy(int? maximumChildren = null)
    {
        var allChildKinds = Enum.GetValues<NeoPageNodeKind>().ToHashSet();
        var definitions = new Dictionary<string, ICompositionCapabilities>(
            StringComparer.OrdinalIgnoreCase)
        {
            ["ui.container"] = CompositionCapabilities.Container(
                allChildKinds,
                ContainerParents,
                maximumChildren),
            ["ui.button"] = CompositionCapabilities.Leaf(ContainerParents.ToArray()),
            ["ui.text"] = CompositionCapabilities.Leaf(ContainerParents.ToArray()),
            ["ui.image"] = CompositionCapabilities.Leaf(ContainerParents.ToArray())
        };

        return new CompositionPolicy(new DictionaryCapabilityResolver(definitions));
    }

    private static NeoPageNode Node(string catalogId, NeoPageNodeKind kind, string nodeId) =>
        new()
        {
            CatalogId = catalogId,
            Kind = kind,
            NodeId = nodeId
        };

    private sealed class DictionaryCapabilityResolver(
        IReadOnlyDictionary<string, ICompositionCapabilities> definitions)
        : ICompositionCapabilityResolver
    {
        public bool TryGet(string catalogId, out ICompositionCapabilities capabilities) =>
            definitions.TryGetValue(catalogId, out capabilities!);
    }
}
