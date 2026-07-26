using System.Text.Json;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Core.Content.Indexing;
using Aero.Cms.Core.Content.Services;
using Aero.Cms.Core.Content.Templating;
using Aero.Cms.Core.Extensions;
using AeroDB.Sable;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Aero.Cms.Core.Tests.Content;

public sealed class RangeAndColorContentFieldTests
{
    [Test]
    public async Task Generated_schema_describes_range_bounds_and_color_shape()
    {
        var definition = Definition();

        using var schema = ContentTypeSchemaGenerator.GenerateSchema(definition);
        var properties = schema.RootElement.GetProperty("properties");
        var range = properties.GetProperty("rating");
        var color = properties.GetProperty("accent");

        await Assert.That(range.GetProperty("type").GetString()).IsEqualTo("integer");
        await Assert.That(range.GetProperty("minimum").GetInt32()).IsEqualTo(-2);
        await Assert.That(range.GetProperty("maximum").GetInt32()).IsEqualTo(5);
        await Assert.That(color.GetProperty("type").GetString()).IsEqualTo("string");
        await Assert.That(color.GetProperty("pattern").GetString()).Contains("[0-9A-Fa-f]");
    }

    [Test]
    public async Task Range_definition_rejects_negative_start_without_permission()
    {
        var session = Substitute.For<IDocumentSession>();
        var service = new AeroContentTypeService(
            session,
            [],
            new ScribanTemplateValidator());
        var definition = Definition();
        definition.Fields[0].Settings[RangeContentFieldSettings.AllowNegative] =
            JsonSerializer.SerializeToElement(false);

        var result = await service.SaveAsync(definition);

        await Assert.That(result.IsFailure).IsTrue();
        await session.DidNotReceive().SaveChangesAsync(
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Range_and_color_validators_accept_picker_values_and_reject_invalid_values()
    {
        var definition = Definition();
        var validators = new IContentFieldValidator[]
        {
            new RangeFieldValidator(),
            new ColorFieldValidator()
        };

        using var validFields = JsonDocument.Parse(
            """{"rating":-1,"accent":"#6633CC80"}""");
        using var invalidFields = JsonDocument.Parse(
            """{"rating":6,"accent":"javascript:alert(1)"}""");

        var valid = new DynamicContentValidator(
                definition,
                ContentValidationMode.Publish,
                validators)
            .Validate(Item(validFields));
        var invalid = new DynamicContentValidator(
                definition,
                ContentValidationMode.Publish,
                validators)
            .Validate(Item(invalidFields));

        await Assert.That(valid.IsValid).IsTrue();
        await Assert.That(invalid.IsValid).IsFalse();
        await Assert.That(invalid.Errors.Select(error => error.PropertyName))
            .Contains("rating");
        await Assert.That(invalid.Errors.Select(error => error.PropertyName))
            .Contains("accent");
    }

    [Test]
    public async Task List_can_explicitly_allow_no_selection_when_publishing()
    {
        var definition = new ContentTypeDefinition
        {
            Alias = "choices",
            Fields =
            [
                new ContentFieldDefinition
                {
                    Name = "tags",
                    FieldType = ContentFieldTypes.List,
                    Required = false,
                    Settings = Settings(
                        (CompositeContentFieldSettings.ItemType, "text"),
                        (CompositeContentFieldSettings.AllowedValues, new[] { "one", "two" }),
                        (CompositeContentFieldSettings.MinimumItems, 0),
                        (CompositeContentFieldSettings.MaximumItems, 2))
                }
            ]
        };
        using var fields = JsonDocument.Parse("""{"tags":[]}""");

        var optional = new DynamicContentValidator(
                definition,
                ContentValidationMode.Publish,
                [new ListFieldValidator()])
            .Validate(Item(fields, "choices"));
        definition.Fields[0].Required = true;
        var required = new DynamicContentValidator(
                definition,
                ContentValidationMode.Publish,
                [new ListFieldValidator()])
            .Validate(Item(fields, "choices"));

        await Assert.That(optional.IsValid).IsTrue();
        await Assert.That(required.IsValid).IsFalse();
    }

    [Test]
    public async Task Content_system_registers_range_and_color_extensions()
    {
        var services = new ServiceCollection();
        services.AddContentTypeSystem();
        using var provider = services.BuildServiceProvider();

        var editors = provider.GetServices<IContentFieldEditor>()
            .Select(service => service.FieldType)
            .ToArray();
        var validators = provider.GetServices<IContentFieldValidator>()
            .Select(service => service.FieldType)
            .ToArray();
        var snippets = provider.GetServices<IFieldTemplateSnippet>()
            .Select(service => service.FieldType)
            .ToArray();
        var indexers = provider.GetServices<IContentFieldIndexer>()
            .Select(service => service.FieldType)
            .ToArray();

        await Assert.That(editors).Contains(ContentFieldTypes.Range);
        await Assert.That(editors).Contains(ContentFieldTypes.Color);
        await Assert.That(validators).Contains(ContentFieldTypes.Range);
        await Assert.That(validators).Contains(ContentFieldTypes.Color);
        await Assert.That(snippets).Contains(ContentFieldTypes.Range);
        await Assert.That(snippets).Contains(ContentFieldTypes.Color);
        await Assert.That(indexers).Contains(ContentFieldTypes.Range);
        await Assert.That(indexers).Contains(ContentFieldTypes.Color);
    }

    [Test]
    public async Task Editors_normalize_range_and_color_to_persistable_scalar_values()
    {
        var range = new RangeFieldEditor().Normalize("4");
        var color = new ColorFieldEditor().Normalize("  #6633CC80  ");

        using var json = JsonDocument.Parse(
            JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["rating"] = range,
                ["accent"] = color
            }));

        await Assert.That(json.RootElement.GetProperty("rating").ValueKind)
            .IsEqualTo(JsonValueKind.Number);
        await Assert.That(json.RootElement.GetProperty("rating").GetInt32())
            .IsEqualTo(4);
        await Assert.That(json.RootElement.GetProperty("accent").ValueKind)
            .IsEqualTo(JsonValueKind.String);
        await Assert.That(json.RootElement.GetProperty("accent").GetString())
            .IsEqualTo("#6633CC80");
    }

    private static ContentTypeDefinition Definition() => new()
    {
        Alias = "style",
        Fields =
        [
            new ContentFieldDefinition
            {
                Name = "rating",
                FieldType = ContentFieldTypes.Range,
                Required = true,
                Settings = Settings(
                    (RangeContentFieldSettings.Start, -2),
                    (RangeContentFieldSettings.End, 5),
                    (RangeContentFieldSettings.AllowNegative, true))
            },
            new ContentFieldDefinition
            {
                Name = "accent",
                FieldType = ContentFieldTypes.Color,
                Required = true
            }
        ]
    };

    private static ContentItem Item(
        JsonDocument fields,
        string alias = "style") => new()
    {
        ContentTypeAlias = alias,
        Slug = "sample",
        Fields = fields.RootElement.EnumerateObject().ToDictionary(
            property => property.Name,
            property => property.Value.Clone())
    };

    private static Dictionary<string, JsonElement> Settings(
        params (string Key, object Value)[] values) =>
        values.ToDictionary(
            pair => pair.Key,
            pair => JsonSerializer.SerializeToElement(pair.Value));
}
