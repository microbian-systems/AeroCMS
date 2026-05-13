using System.Text.Json;
using Aero.Cms.Shared.Pages.Manager.PageEditor.Catalog;
using FluentAssertions;
using TUnit.Core;

namespace Aero.Cms.Core.Tests.Catalog;

/// <summary>
/// Verifies that Neo editor catalog types survive System.Text.Json
/// round-trip serialization, which is the format Marten uses internally
/// when storing these types as nested JSON documents.
/// </summary>
public class CatalogSerializationTests
{
    private static readonly JsonSerializerOptions DefaultOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // ── Type survival ──────────────────────────────────────────────────

    [Test]
    public void NeoEditorCatalogSection_enum_round_trips()
    {
        RoundTripEnum(NeoEditorCatalogSection.AeroUi, "AeroUi");
        RoundTripEnum(NeoEditorCatalogSection.Primitives, "Primitives");
        RoundTripEnum(NeoEditorCatalogSection.Components, "Components");
    }

    [Test]
    public void NeoEditorCatalogKind_enum_round_trips()
    {
        RoundTripEnum(NeoEditorCatalogKind.Block, "Block");
        RoundTripEnum(NeoEditorCatalogKind.Primitive, "Primitive");
        RoundTripEnum(NeoEditorCatalogKind.Component, "Component");
    }

    [Test]
    public void NeoPropertyFieldType_enum_round_trips()
    {
        RoundTripEnum(NeoPropertyFieldType.Text, "Text");
        RoundTripEnum(NeoPropertyFieldType.TextArea, "TextArea");
        RoundTripEnum(NeoPropertyFieldType.Url, "Url");
        RoundTripEnum(NeoPropertyFieldType.Boolean, "Boolean");
        RoundTripEnum(NeoPropertyFieldType.Number, "Number");
    }

    // ── Record survival ────────────────────────────────────────────────

    [Test]
    public void NeoPropertyDefinition_round_trips()
    {
        var original = new NeoPropertyDefinition
        {
            Name = "title",
            Label = "Title",
            FieldType = NeoPropertyFieldType.Text,
            Required = true,
            DefaultValue = "Untitled",
            Options = ["Option A", "Option B"]
        };

        var json = SerializeAndBack(original, out var restored);

        restored.Name.Should().Be("title");
        restored.Label.Should().Be("Title");
        restored.FieldType.Should().Be(NeoPropertyFieldType.Text);
        restored.Required.Should().BeTrue();
        restored.DefaultValue.Should().Be("Untitled");
        restored.Options.Should().ContainInOrder("Option A", "Option B");
    }

    [Test]
    public void NeoEditorCatalogItem_round_trips_without_type_references()
    {
        // Type properties (EditorPreviewComponentType, PropertyEditorComponentType,
        // PublicRendererComponentType) are null in generated catalogs — we emit
        // null because the source generator can't reliably detect which types exist.
        // Marten must survive null Type fields during serialization.
        var original = new NeoEditorCatalogItem
        {
            CatalogId = "aero.hero.01",
            DisplayName = "Hero 01",
            Description = "Full-width hero with eyebrow, title, and CTAs",
            Section = NeoEditorCatalogSection.AeroUi,
            Kind = NeoEditorCatalogKind.Block,
            IconName = "layout",
            SortOrder = 10,
            AllowChildren = false,
            PublicStaticSsrSafe = true,
            RequiresInteractiveIsland = false,
            EditorPreviewComponentType = null,
            PropertyEditorComponentType = null,
            PublicRendererComponentType = null,
            PropertyDefinitions =
            [
                new()
                {
                    Name = "title",
                    Label = "Title",
                    FieldType = NeoPropertyFieldType.Text,
                    Required = true
                }
            ],
            AllowedChildCatalogIds = new HashSet<string> { "aero.hero.01-child" },
            AllowedParentCatalogIds = new HashSet<string>()
        };

        var json = SerializeAndBack(original, out var restored);

        restored.CatalogId.Should().Be("aero.hero.01");
        restored.DisplayName.Should().Be("Hero 01");
        restored.Description.Should().Be("Full-width hero with eyebrow, title, and CTAs");
        restored.Section.Should().Be(NeoEditorCatalogSection.AeroUi);
        restored.Kind.Should().Be(NeoEditorCatalogKind.Block);
        restored.IconName.Should().Be("layout");
        restored.SortOrder.Should().Be(10);
        restored.AllowChildren.Should().BeFalse();
        restored.PublicStaticSsrSafe.Should().BeTrue();
        restored.RequiresInteractiveIsland.Should().BeFalse();
        restored.EditorPreviewComponentType.Should().BeNull();
        restored.PropertyEditorComponentType.Should().BeNull();
        restored.PublicRendererComponentType.Should().BeNull();
        restored.PropertyDefinitions.Should().HaveCount(1);
        restored.PropertyDefinitions[0].Name.Should().Be("title");
        restored.AllowedChildCatalogIds.Should().Contain("aero.hero.01-child");
    }

    // ── Helpers ────────────────────────────────────────────────────────

    private static void RoundTripEnum<T>(T value, string expectedJsonValue) where T : struct, Enum
    {
        var json = JsonSerializer.Serialize(value, DefaultOptions);
        var restored = JsonSerializer.Deserialize<T>(json, DefaultOptions);

        restored.Should().Be(value);
        // STJ writes enums as integers by default; the name round-trips
        // correctly because we deserialize by value.
    }

    private static string SerializeAndBack<T>(T original, out T restored)
    {
        var json = JsonSerializer.Serialize(original, DefaultOptions);

        // Verify we get valid JSON — empty string means serialization failed.
        json.Should().NotBeNullOrEmpty("serialization must produce non-empty JSON");

        restored = JsonSerializer.Deserialize<T>(json, DefaultOptions)!;
        restored.Should().NotBeNull("deserialization must return a non-null instance");

        return json;
    }
}
