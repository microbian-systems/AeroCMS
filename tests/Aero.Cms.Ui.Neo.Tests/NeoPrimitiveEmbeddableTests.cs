using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo.Composition;
using Aero.Cms.Ui.Neo;
using FluentAssertions;
using TUnit.Core;

namespace Aero.Cms.Ui.Neo.Tests;

public sealed class NeoPrimitiveEmbeddableTests
{
    private static readonly string[] ExpectedCatalogIds =
    [
        "primitive.container",
        "primitive.text",
        "primitive.button",
        "primitive.image",
        "primitive.pill",
        "primitive.icon",
        "primitive.separator",
        "preset.card"
    ];

    [Test]
    public void AllNeoPrimitiveDescriptors_ImplementIEmbeddable_AndAreEmbeddable()
    {
        var provider = new NeoPageEditorBlockProvider();
        var descriptors = provider.GetEditorDefinitions();

        descriptors.Should().HaveCount(ExpectedCatalogIds.Length);

        var actualCatalogIds = descriptors.Select(d => d.CatalogId).OrderBy(id => id).ToArray();
        var expectedCatalogIds = ExpectedCatalogIds.OrderBy(id => id).ToArray();
        actualCatalogIds.Should().BeEquivalentTo(expectedCatalogIds,
            "because the 8 primitive descriptors should be the only editor definitions");

        foreach (var descriptor in descriptors)
        {
            descriptor.Catalog.Should().BeAssignableTo<IEmbeddable>(
                $"because primitive '{descriptor.CatalogId}' ({descriptor.Catalog.DisplayName}) should be embeddable");

            descriptor.Catalog.Composition.IsEmbeddable.Should().BeTrue(
                $"because primitive '{descriptor.CatalogId}' should have IsEmbeddable == true");
        }
    }
}
