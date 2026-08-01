using System.Text.Json;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Content.Serialization;
using Aero.Cms.Core.Content.Services;

namespace Aero.Cms.Core.Tests.Content;

public sealed class ReferenceFieldValidatorTests
{
    [Test]
    [Arguments(ContentValidationMode.Draft)]
    [Arguments(ContentValidationMode.Publish)]
    public async Task Optional_single_reference_accepts_an_empty_editor_value(ContentValidationMode mode)
    {
        var result = Validate(
            new ContentFieldDefinition
            {
                Name = "parent",
                Label = "Parent",
                FieldType = "reference"
            },
            JsonSerializer.SerializeToElement(string.Empty),
            mode);

        await Assert.That(result.IsValid).IsTrue();
    }

    [Test]
    public async Task Required_single_reference_rejects_an_empty_value_when_publishing()
    {
        var result = Validate(
            new ContentFieldDefinition
            {
                Name = "parent",
                Label = "Parent",
                FieldType = "reference",
                Required = true
            },
            JsonSerializer.SerializeToElement(string.Empty),
            ContentValidationMode.Publish);

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors.Select(error => error.ErrorMessage)).Contains("Parent is required.");
    }

    [Test]
    public async Task Required_multiple_reference_rejects_an_empty_list_when_publishing()
    {
        var result = Validate(
            new ContentFieldDefinition
            {
                Name = "related",
                Label = "Related",
                FieldType = "reference",
                Required = true,
                Settings = new Dictionary<string, JsonElement>
                {
                    ["allowMultiple"] = JsonSerializer.SerializeToElement(true)
                }
            },
            JsonSerializer.SerializeToElement(Array.Empty<string>()),
            ContentValidationMode.Publish);

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors.Select(error => error.ErrorMessage)).Contains("Related is required.");
    }

    [Test]
    public async Task Cms_document_reference_accepts_a_typed_page_selection()
    {
        var result = Validate(
            CmsDocumentReference(),
            JsonSerializer.SerializeToElement(
                new CmsContentReferenceValue(
                    CmsContentReferenceSources.Pages,
                    "1530221140281556994"),
                ContentJsonContext.Default.CmsContentReferenceValue),
            ContentValidationMode.Publish);

        await Assert.That(result.IsValid).IsTrue();
    }

    [Test]
    public async Task Cms_document_reference_rejects_a_source_outside_its_allow_list()
    {
        var field = CmsDocumentReference();
        field.Settings[ReferenceContentFieldSettings.AllowedSources] =
            JsonSerializer.SerializeToElement(
                new[] { CmsContentReferenceSources.Pages });

        var result = Validate(
            field,
            JsonSerializer.SerializeToElement(
                new CmsContentReferenceValue(
                    CmsContentReferenceSources.Posts,
                    "1530221140281556994"),
                ContentJsonContext.Default.CmsContentReferenceValue),
            ContentValidationMode.Publish);

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors.Select(error => error.ErrorMessage))
            .Contains("Related content uses an unsupported content source.");
    }

    [Test]
    public async Task Cms_document_reference_accepts_a_public_content_entry_source()
    {
        var result = Validate(
            CmsDocumentReference(),
            JsonSerializer.SerializeToElement(
                new CmsContentReferenceValue(
                    CmsContentReferenceSources.ForContentType("species"),
                    "1530221140281556994"),
                ContentJsonContext.Default.CmsContentReferenceValue),
            ContentValidationMode.Publish);

        await Assert.That(result.IsValid).IsTrue();
    }

    [Test]
    public async Task Cms_document_reference_rejects_a_public_content_entry_when_not_allowed()
    {
        var field = CmsDocumentReference();
        field.Settings[ReferenceContentFieldSettings.AllowedSources] =
            JsonSerializer.SerializeToElement(
                new[] { CmsContentReferenceSources.Pages });

        var result = Validate(
            field,
            JsonSerializer.SerializeToElement(
                new CmsContentReferenceValue(
                    CmsContentReferenceSources.ForContentType("species"),
                    "1530221140281556994"),
                ContentJsonContext.Default.CmsContentReferenceValue),
            ContentValidationMode.Publish);

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors.Select(error => error.ErrorMessage))
            .Contains("Related content uses an unsupported content source.");
    }

    [Test]
    public async Task Optional_cms_document_reference_accepts_a_null_selection()
    {
        var result = Validate(
            CmsDocumentReference(),
            JsonSerializer.SerializeToElement<object?>(null),
            ContentValidationMode.Publish);

        await Assert.That(result.IsValid).IsTrue();
    }

    private static ContentFieldDefinition CmsDocumentReference() =>
        new()
        {
            Name = "related-content",
            Label = "Related content",
            FieldType = ContentFieldTypes.Reference,
            Settings = new Dictionary<string, JsonElement>
            {
                [ReferenceContentFieldSettings.TargetKind] =
                    JsonSerializer.SerializeToElement(
                        ReferenceContentFieldSettings.TargetKindCmsDocument),
                [ReferenceContentFieldSettings.AllowedSources] =
                    JsonSerializer.SerializeToElement(
                        CmsContentReferenceSources.All.ToArray())
            }
        };

    private static FluentValidation.Results.ValidationResult Validate(
        ContentFieldDefinition field,
        JsonElement value,
        ContentValidationMode mode)
    {
        var definition = new ContentTypeDefinition
        {
            Alias = "animal",
            Fields = [field]
        };
        var item = new ContentItem
        {
            ContentTypeAlias = definition.Alias,
            Slug = "example",
            Fields = new Dictionary<string, JsonElement>
            {
                [field.Name] = value
            }
        };

        return new DynamicContentValidator(definition, mode, [new ReferenceFieldValidator()]).Validate(item);
    }
}
