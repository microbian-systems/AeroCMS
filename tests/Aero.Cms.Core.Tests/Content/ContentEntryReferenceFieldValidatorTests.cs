using System.Text.Json;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Content.Serialization;
using Aero.Cms.Abstractions.Content.Views;
using Aero.Cms.Core.Content;
using Aero.Cms.Core.Content.Services;
using Aero.Cms.Core.Infrastructure;
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
    public async Task Query_backed_reference_accepts_ordered_preview_fields()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<ContentTypeDocument>(SchemaMode.Flexible);
        await harness.InitializeAsync();
        var service = new AeroContentTypeService(
            harness.Session,
            [],
            new Aero.Cms.Core.Content.Templating.ScribanTemplateValidator());
        var field = ContentEntryReference("view:catalog");
        field.Settings[ReferenceContentFieldSettings.PreviewFields] =
            JsonSerializer.SerializeToElement(new[] { "commonName", "scientificName" });

        var result = await service.SaveAsync(new ContentTypeDefinition
        {
            SiteId = 1,
            Alias = "animal",
            Name = "Animal",
            Fields = [field]
        });

        result.IsSuccess.ShouldBeTrue();
    }

    [Test]
    public async Task Preview_fields_reject_invalid_shape_duplicates_and_non_query_references()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<ContentTypeDocument>(SchemaMode.Flexible);
        await harness.InitializeAsync();
        var service = new AeroContentTypeService(
            harness.Session,
            [],
            new Aero.Cms.Core.Content.Templating.ScribanTemplateValidator());

        var duplicate = ContentEntryReference();
        duplicate.Settings[ReferenceContentFieldSettings.PreviewFields] =
            JsonSerializer.SerializeToElement(new[] { "name", "name" });
        var tooMany = ContentEntryReference();
        tooMany.Settings[ReferenceContentFieldSettings.PreviewFields] =
            JsonSerializer.SerializeToElement(Enumerable.Range(0, 17).Select(index => $"field{index}").ToArray());
        var invalidValues = ContentEntryReference();
        invalidValues.Settings[ReferenceContentFieldSettings.PreviewFields] =
            JsonSerializer.SerializeToElement(new object?[] { " ", new string('a', 129), 7 });
        var cmsDocument = new ContentFieldDefinition
        {
            Name = "related",
            FieldType = ContentFieldTypes.Reference,
            Settings = new Dictionary<string, JsonElement>
            {
                [ReferenceContentFieldSettings.TargetKind] = JsonSerializer.SerializeToElement(ReferenceContentFieldSettings.TargetKindCmsDocument),
                [ReferenceContentFieldSettings.AllowedSources] = JsonSerializer.SerializeToElement(new[] { CmsContentReferenceSources.Pages }),
                [ReferenceContentFieldSettings.PreviewFields] = JsonSerializer.SerializeToElement(new[] { "title" })
            }
        };

        var duplicateResult = await service.SaveAsync(Definition("duplicate", duplicate));
        var tooManyResult = await service.SaveAsync(Definition("too-many", tooMany));
        var invalidValuesResult = await service.SaveAsync(Definition("invalid-values", invalidValues));
        var cmsDocumentResult = await service.SaveAsync(Definition("cms-document", cmsDocument));

        duplicateResult.IsFailure.ShouldBeTrue();
        tooManyResult.IsFailure.ShouldBeTrue();
        invalidValuesResult.IsFailure.ShouldBeTrue();
        cmsDocumentResult.IsFailure.ShouldBeTrue();
    }
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
        site.SiteId.Returns(43);
        var selectedSites = Substitute.For<ISelectedSiteScopeResolver>();
        selectedSites.ResolveAsync(43, Arg.Any<CancellationToken>())
            .Returns(new SelectedSiteScope(71, 43));
        var provider = new RecordingProvider("view:catalog");
        var validator = new ReferenceExistenceValidator(
            Substitute.For<IContentService>(),
            Substitute.For<IDocumentSession>(),
            entryProviders: [provider],
            siteContext: site,
            selectedSiteScopeResolver: selectedSites);
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

    [Test]
    public async Task Background_validation_uses_authoritative_site_scope_without_http_context()
    {
        var selectedSites = Substitute.For<ISelectedSiteScopeResolver>();
        selectedSites.ResolveAsync(43, Arg.Any<CancellationToken>())
            .Returns(new SelectedSiteScope(71, 43));
        var provider = new RecordingProvider("view:catalog");
        var validator = new ReferenceExistenceValidator(
            Substitute.For<IContentService>(),
            Substitute.For<IDocumentSession>(),
            entryProviders: [provider],
            selectedSiteScopeResolver: selectedSites);
        var item = new ContentItem
        {
            SiteId = 43,
            ContentTypeAlias = "article",
            Slug = "example",
            Fields = new Dictionary<string, JsonElement>
            {
                ["related"] = JsonSerializer.SerializeToElement(
                    new ContentEntryKey("view:catalog", "entry-42"),
                    ContentJsonContext.Default.ContentEntryKey)
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

    private static ContentTypeDefinition Definition(
        string alias,
        ContentFieldDefinition field) => new()
        {
            SiteId = 1,
            Alias = alias,
            Name = alias,
            Fields = [field]
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
