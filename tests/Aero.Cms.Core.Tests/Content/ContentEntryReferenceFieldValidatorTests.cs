using System.Text.Json;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Content.Serialization;
using Aero.Cms.Abstractions.Content.Views;
using Aero.Cms.Core.Content.Services;
using Aero.Core;
using Aero.Core.Http;
using Aero.Core.Railway;
using AeroDB.Sable;
using NSubstitute;
using Shouldly;

namespace Aero.Cms.Core.Tests.Content;

public sealed class ContentEntryReferenceFieldValidatorTests
{
    [Test]
    public void Valid_provider_qualified_entry_is_accepted()
    {
        var result = Validate(
            ContentEntryReference(),
            new ContentEntryKey("view:catalog", "entry-42"));

        result.IsValid.ShouldBeTrue();
    }

    [Test]
    public void Provider_outside_field_allow_list_is_rejected()
    {
        var result = Validate(
            ContentEntryReference("view:catalog"),
            new ContentEntryKey("view:articles", "entry-42"));

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.ErrorMessage)
            .ShouldContain("Related entry uses an unsupported content-entry provider.");
    }

    [Test]
    public void Numeric_cms_document_contract_is_not_accepted_as_a_virtual_entry()
    {
        var result = new DynamicContentValidator(
            new ContentTypeDefinition { Alias = "article", Fields = [ContentEntryReference()] },
            ContentValidationMode.Publish,
            [new ReferenceFieldValidator()])
            .Validate(new ContentItem
            {
                ContentTypeAlias = "article",
                Slug = "example",
                Fields = new Dictionary<string, JsonElement>
                {
                    ["related"] = JsonSerializer.SerializeToElement(
                        new CmsContentReferenceValue(CmsContentReferenceSources.Pages, "42"),
                        ContentJsonContext.Default.CmsContentReferenceValue)
                }
            });

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.ErrorMessage)
            .ShouldContain("Related entry must select a valid content entry.");
    }

    [Test]
    public async Task Missing_virtual_entry_blocks_save_with_the_server_resolved_scope()
    {
        var site = Substitute.For<ISiteContext>();
        site.TenantId.Returns(71);
        var provider = new RecordingProvider("view:catalog");
        var validator = new ReferenceExistenceValidator(
            Substitute.For<IContentService>(),
            Substitute.For<IDocumentSession>(),
            entryProviders: [provider],
            siteContext: site);
        var key = new ContentEntryKey("view:catalog", "entry-42");
        var item = new ContentItem
        {
            SiteId = 43,
            ContentTypeAlias = "article",
            Slug = "example",
            Fields = new Dictionary<string, JsonElement>
            {
                ["related"] = JsonSerializer.SerializeToElement(key, ContentJsonContext.Default.ContentEntryKey)
            }
        };

        var failures = await validator.ValidateAsync(
            item,
            new ContentTypeDefinition { Alias = "article", Fields = [ContentEntryReference("view:catalog")] },
            CancellationToken.None);

        provider.LastScope.ShouldBe(new ContentViewScope(71, 43));
        failures.Select(failure => failure.ErrorMessage)
            .ShouldContain("Referenced content entry 'entry-42' was not found.");
    }

    private static ContentFieldDefinition ContentEntryReference(params string[] providers) =>
        new()
        {
            Name = "related",
            Label = "Related entry",
            FieldType = ContentFieldTypes.Reference,
            Settings = new Dictionary<string, JsonElement>
            {
                [ReferenceContentFieldSettings.TargetKind] = JsonSerializer.SerializeToElement(
                    ReferenceContentFieldSettings.TargetKindContentEntry),
                [ReferenceContentFieldSettings.AllowedProviders] = JsonSerializer.SerializeToElement(providers)
            }
        };

    private static FluentValidation.Results.ValidationResult Validate(
        ContentFieldDefinition field,
        ContentEntryKey value) =>
        new DynamicContentValidator(
            new ContentTypeDefinition { Alias = "article", Fields = [field] },
            ContentValidationMode.Publish,
            [new ReferenceFieldValidator()])
            .Validate(new ContentItem
            {
                ContentTypeAlias = "article",
                Slug = "example",
                Fields = new Dictionary<string, JsonElement>
                {
                    [field.Name] = JsonSerializer.SerializeToElement(
                        value,
                        ContentJsonContext.Default.ContentEntryKey)
                }
            });

    private sealed class RecordingProvider(string provider) : IContentEntrySourceProvider
    {
        public string Provider => provider;
        public ContentViewScope? LastScope { get; private set; }

        public Task<ContentEntry?> FindAsync(ContentViewScope scope, string stableId, CancellationToken ct = default)
        {
            LastScope = scope;
            return Task.FromResult<ContentEntry?>(null);
        }

        public Task<IReadOnlyList<ContentEntry>> SearchAsync(ContentViewScope scope, string? culture, string? query, int take, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ContentEntry>>([]);
    }
}
