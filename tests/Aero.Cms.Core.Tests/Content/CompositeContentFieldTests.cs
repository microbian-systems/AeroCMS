using System.Text.Json;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Core.Content.Services;
using Aero.Cms.Core.Content.Templating;
using Aero.Cms.Core.Extensions;
using AeroDB.Sable;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Aero.Cms.Core.Tests.Content;

public sealed class CompositeContentFieldTests
{
    [Test]
    public async Task Generated_schema_describes_array_and_object_shapes()
    {
        var definition = CreateDefinition();

        using var schema = ContentTypeSchemaGenerator.GenerateSchema(definition);
        var properties = schema.RootElement.GetProperty("properties");

        await Assert.That(properties.GetProperty("tags").GetProperty("type").GetString()).IsEqualTo("array");
        await Assert.That(properties.GetProperty("tags").GetProperty("items").GetProperty("type").GetString()).IsEqualTo("string");
        await Assert.That(properties.GetProperty("tags").GetProperty("items").GetProperty("enum").GetArrayLength()).IsEqualTo(2);
        await Assert.That(properties.GetProperty("photos").GetProperty("type").GetString()).IsEqualTo("array");
        await Assert.That(properties.GetProperty("facts").GetProperty("type").GetString()).IsEqualTo("object");
        await Assert.That(properties.GetProperty("facts").GetProperty("additionalProperties").GetProperty("type").GetString()).IsEqualTo("number");
    }

    [Test]
    public async Task Required_composite_fields_reject_empty_values_when_publishing()
    {
        var definition = CreateDefinition();
        foreach (var field in definition.Fields) field.Required = true;
        using var values = JsonDocument.Parse("""{"tags":[],"photos":[],"facts":{}}""");
        var validator = CreateValidator(definition, ContentValidationMode.Publish);

        var result = validator.Validate(CreateItem(values));

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors.Select(error => error.PropertyName)).Contains("tags");
        await Assert.That(result.Errors.Select(error => error.PropertyName)).Contains("photos");
        await Assert.That(result.Errors.Select(error => error.PropertyName)).Contains("facts");
    }

    [Test]
    public async Task Optional_list_accepts_no_selection_even_with_a_configured_minimum()
    {
        var definition = CreateDefinition();
        definition.Fields[0].Settings[CompositeContentFieldSettings.MinimumItems] =
            JsonSerializer.SerializeToElement(2);
        using var values = JsonDocument.Parse("""{"tags":[],"photos":[],"facts":{}}""");

        var result = CreateValidator(
                definition,
                ContentValidationMode.Publish)
            .Validate(CreateItem(values));

        await Assert.That(result.IsValid).IsTrue();
    }

    [Test]
    public async Task Required_list_may_remain_empty_in_a_draft()
    {
        var definition = CreateDefinition();
        definition.Fields[0].Required = true;
        definition.Fields[0].Settings[CompositeContentFieldSettings.MinimumItems] =
            JsonSerializer.SerializeToElement(2);
        using var values = JsonDocument.Parse("""{"tags":[],"photos":[],"facts":{}}""");

        var result = CreateValidator(
                definition,
                ContentValidationMode.Draft)
            .Validate(CreateItem(values));

        await Assert.That(result.IsValid).IsTrue();
    }

    [Test]
    public async Task Generated_schema_keeps_optional_lists_empty_and_required_lists_nonempty()
    {
        var definition = CreateDefinition();
        definition.Fields[0].Settings[CompositeContentFieldSettings.MinimumItems] =
            JsonSerializer.SerializeToElement(2);

        using var optionalSchema = ContentTypeSchemaGenerator.GenerateSchema(definition);
        var optionalMinimum = optionalSchema.RootElement
            .GetProperty("properties")
            .GetProperty("tags")
            .GetProperty("minItems")
            .GetInt32();
        var optionalAlternatives = optionalSchema.RootElement
            .GetProperty("properties")
            .GetProperty("tags")
            .GetProperty("anyOf");

        definition.Fields[0].Required = true;
        using var requiredSchema = ContentTypeSchemaGenerator.GenerateSchema(definition);
        var requiredMinimum = requiredSchema.RootElement
            .GetProperty("properties")
            .GetProperty("tags")
            .GetProperty("minItems")
            .GetInt32();

        await Assert.That(optionalMinimum).IsEqualTo(0);
        await Assert.That(optionalAlternatives[0].GetProperty("maxItems").GetInt32())
            .IsEqualTo(0);
        await Assert.That(optionalAlternatives[1].GetProperty("minItems").GetInt32())
            .IsEqualTo(2);
        await Assert.That(requiredMinimum).IsEqualTo(2);
    }

    [Test]
    public async Task Registered_composite_snippets_generate_valid_scriban()
    {
        var services = new ServiceCollection();
        services.AddContentTypeSystem();
        using var provider = services.BuildServiceProvider();
        var snippets = provider.GetServices<IFieldTemplateSnippet>();
        var template = ContentTypeTemplateGenerator.GenerateTemplate(CreateDefinition(), snippets);

        var result = new ScribanTemplateValidator().Validate(template);

        await Assert.That(result.IsSuccess).IsTrue();
    }

    [Test]
    public async Task Invalid_composite_definitions_fail_before_persistence()
    {
        var session = Substitute.For<IDocumentSession>();
        var service = new AeroContentTypeService(session, [], new ScribanTemplateValidator());
        var definition = CreateDefinition();
        definition.Fields[0].Settings[CompositeContentFieldSettings.AllowedValues] =
            JsonSerializer.SerializeToElement(new object[] { "one", 2 });

        var result = await service.SaveAsync(definition);
        definition.Fields[0].Settings = null!;
        var nullSettingsResult = await service.SaveAsync(definition);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(nullSettingsResult.IsFailure).IsTrue();
        await session.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task List_definition_rejects_a_minimum_larger_than_its_unique_choices()
    {
        var session = Substitute.For<IDocumentSession>();
        var service = new AeroContentTypeService(session, [], new ScribanTemplateValidator());
        var definition = CreateDefinition();
        definition.Fields[0].Settings[CompositeContentFieldSettings.MinimumItems] = JsonSerializer.SerializeToElement(3);
        definition.Fields[0].Settings[CompositeContentFieldSettings.MaximumItems] = JsonSerializer.SerializeToElement(5);

        var result = await service.SaveAsync(definition);

        await Assert.That(result.IsFailure).IsTrue();
        await session.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Numeric_list_treats_equivalent_scales_as_duplicates()
    {
        var definition = CreateDefinition();
        definition.Fields[0].Settings = Settings(
            (CompositeContentFieldSettings.ItemType, "number"),
            (CompositeContentFieldSettings.AllowedValues, new[] { "1" }),
            (CompositeContentFieldSettings.MaximumItems, 2));
        using var values = JsonDocument.Parse("""{"tags":[1,1.0],"photos":[],"facts":{}}""");

        var result = CreateValidator(definition).Validate(CreateItem(values));

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors.Select(error => error.PropertyName)).Contains("tags");
    }

    [Test]
    public async Task Composite_validators_accept_bounded_typed_values()
    {
        var definition = CreateDefinition();
        using var values = JsonDocument.Parse("""{"tags":["one","two"],"photos":["/media/a.jpg"],"facts":{"calories":120}}""");
        var item = CreateItem(values);
        var validator = CreateValidator(definition);

        var result = validator.Validate(item);

        await Assert.That(result.IsValid).IsTrue();
    }

    [Test]
    public async Task Composite_validators_reject_unknown_choices_and_wrong_value_types()
    {
        var definition = CreateDefinition();
        using var values = JsonDocument.Parse("""{"tags":["other"],"photos":[3],"facts":{"calories":"many"}}""");
        var item = CreateItem(values);
        var validator = CreateValidator(definition);

        var result = validator.Validate(item);

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors.Select(error => error.PropertyName)).Contains("tags");
        await Assert.That(result.Errors.Select(error => error.PropertyName)).Contains("photos");
        await Assert.That(result.Errors.Select(error => error.PropertyName)).Contains("facts");
    }

    private static DynamicContentValidator CreateValidator(
        ContentTypeDefinition definition,
        ContentValidationMode mode = ContentValidationMode.Draft) =>
        new(definition, mode,
        [
            new ListFieldValidator(),
            new GalleryFieldValidator(),
            new DictionaryFieldValidator()
        ]);

    private static ContentTypeDefinition CreateDefinition() => new()
    {
        Alias = "product",
        Fields =
        [
            new ContentFieldDefinition
            {
                Name = "tags",
                FieldType = ContentFieldTypes.List,
                Settings = Settings(
                    (CompositeContentFieldSettings.ItemType, "text"),
                    (CompositeContentFieldSettings.AllowedValues, new[] { "one", "two" }),
                    (CompositeContentFieldSettings.MaximumItems, 2))
            },
            new ContentFieldDefinition
            {
                Name = "photos",
                FieldType = ContentFieldTypes.Gallery,
                Settings = Settings((CompositeContentFieldSettings.MaximumItems, 4))
            },
            new ContentFieldDefinition
            {
                Name = "facts",
                FieldType = ContentFieldTypes.Dictionary,
                Settings = Settings(
                    (CompositeContentFieldSettings.ValueType, "number"),
                    (CompositeContentFieldSettings.MaximumEntries, 5))
            }
        ]
    };

    private static ContentItem CreateItem(JsonDocument values) => new()
    {
        ContentTypeAlias = "product",
        Slug = "sample",
        Fields = values.RootElement.EnumerateObject().ToDictionary(
            property => property.Name,
            property => property.Value.Clone())
    };

    private static Dictionary<string, JsonElement> Settings(params (string Key, object Value)[] values) =>
        values.ToDictionary(
            pair => pair.Key,
            pair => JsonSerializer.SerializeToElement(pair.Value));
}
