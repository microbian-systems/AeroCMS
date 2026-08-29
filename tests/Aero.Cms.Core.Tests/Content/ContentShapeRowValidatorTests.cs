using Aero.Cms.Abstractions.Content.Views;
using Aero.Cms.Core.Content.Views;
using Shouldly;
using System.Text.Json;

namespace Aero.Cms.Core.Tests.Content;

public sealed class ContentShapeRowValidatorTests
{
    [Test]
    public void Rejects_missing_required_nested_and_scalar_mismatches()
    {
        var shape = new ContentShapeDefinition("entry",
        [
            new("id", ContentShapeFieldType.String, Required: true),
            new("profile", ContentShapeFieldType.Object, Required: true, Fields: [new("name", ContentShapeFieldType.String, Required: true)]),
            new("scores", ContentShapeFieldType.List, Item: new("score", ContentShapeFieldType.Number))
        ], "unused");

        var missing = ContentShapeRowValidator.TryValidateRows([new Dictionary<string, object?> { ["profile"] = new Dictionary<string, object?>() }], shape, out _);
        var nested = ContentShapeRowValidator.TryValidateRows([new Dictionary<string, object?> { ["id"] = "one", ["profile"] = new Dictionary<string, object?> { ["name"] = 42 }, ["scores"] = new object?[] { 1 } }], shape, out _);
        var list = ContentShapeRowValidator.TryValidateRows([new Dictionary<string, object?> { ["id"] = "one", ["profile"] = new Dictionary<string, object?> { ["name"] = "ok" }, ["scores"] = new object?[] { "not-a-number" } }], shape, out _);

        missing.ShouldBeFalse();
        nested.ShouldBeFalse();
        list.ShouldBeFalse();
    }

    [Test]
    public void Rejects_undeclared_top_level_and_nested_fields()
    {
        var shape = new ContentShapeDefinition("entry",
        [
            new("id", ContentShapeFieldType.String, Required: true),
            new("profile", ContentShapeFieldType.Object, Fields: [new("name", ContentShapeFieldType.String)])
        ], "unused");

        ContentShapeRowValidator.TryValidateRows([new Dictionary<string, object?> { ["id"] = "one", ["private"] = "secret" }], shape, out _).ShouldBeFalse();
        ContentShapeRowValidator.TryValidateRows([new Dictionary<string, object?> { ["id"] = "one", ["profile"] = new Dictionary<string, object?> { ["name"] = "visible", ["private"] = "secret" } }], shape, out _).ShouldBeFalse();
    }

    [Test]
    public void Accepts_json_arrays_nested_objects_and_reference_keys()
    {
        using var json = JsonDocument.Parse("""{"id":"one","profile":{"name":"visible"},"related":{"provider":"view:catalog","stableId":"entry-42"},"scores":[1,2]}""");
        var shape = new ContentShapeDefinition("entry",
        [
            new("id", ContentShapeFieldType.String, Required: true),
            new("profile", ContentShapeFieldType.Object, Fields: [new("name", ContentShapeFieldType.String)]),
            new("related", ContentShapeFieldType.Reference, ReferenceShapeAlias: "entry"),
            new("scores", ContentShapeFieldType.List, Item: new("score", ContentShapeFieldType.Number))
        ], "unused");
        var row = json.RootElement.EnumerateObject().ToDictionary(item => item.Name, item => (object?)item.Value.Clone());

        ContentShapeRowValidator.TryValidateRows([row], shape, out _).ShouldBeTrue();
    }
}
