using System.Text.Json;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Content.Localization;
using Aero.Cms.Abstractions.Content.Serialization;
using Shouldly;

namespace Aero.Cms.Abstractions.Tests;

public sealed class ContentLocalizationContractsTests
{
    [Test]
    public void New_content_schema_and_fields_fail_closed_for_localization()
    {
        var definition = new ContentTypeDefinition();
        var field = new ContentFieldDefinition();

        definition.Localization.CultureFallbackPolicy.ShouldBe(ContentCultureFallbackPolicy.ExactOnly);
        definition.Localization.AiTranslationReviewPolicy.ShouldBe(ContentAiTranslationReviewPolicy.RequireHumanReview);
        field.LocalizationMode.ShouldBe(ContentFieldLocalizationMode.CopyOnFork);
    }

    [Test]
    public void Localization_contracts_use_explicit_camel_case_string_values_when_serialized()
    {
        var provenance = new ContentTranslationProvenance(
            ContentTranslationOrigin.AiAssisted,
            "en-US",
            4,
            DateTimeOffset.UnixEpoch,
            "provider",
            "model");
        var review = ContentTranslationReview.Pending();

        var provenanceJson = JsonSerializer.Serialize(
            provenance,
            ContentJsonContext.Default.ContentTranslationProvenance);
        var reviewJson = JsonSerializer.Serialize(
            review,
            ContentJsonContext.Default.ContentTranslationReview);

        provenanceJson.ShouldContain("\"origin\":\"aiAssisted\"");
        reviewJson.ShouldContain("\"status\":\"pending\"");
    }

    [Test]
    public void Localization_context_keeps_culture_inputs_explicit()
    {
        var context = new ContentLocalizationContext(17, "en-US", ["en-US", "fr-FR"], ContentCultureFallbackPolicy.ParentCultureThenDefaultCulture);

        context.SiteId.ShouldBe(17);
        context.SupportedCultures.ShouldBe(["en-US", "fr-FR"]);
        context.CultureFallbackPolicy.ShouldBe(ContentCultureFallbackPolicy.ParentCultureThenDefaultCulture);
    }

    [Test]
    public void Translation_group_owns_a_non_null_shared_field_bag_and_revision()
    {
        var group = new ContentTranslationGroup();

        group.SharedFields.ShouldNotBeNull();
        group.SharedFields.ShouldBeEmpty();
        group.Revision.ShouldBe(0);

        var json = JsonSerializer.Serialize(group, ContentJsonContext.Default.ContentTranslationGroup);
        json.ShouldContain("\"sharedFields\":{}");
    }

    [Test]
    public void Ai_translation_command_binds_source_and_target_revisions()
    {
        var command = new ApplyContentAiTranslationCommand(
            SourceItemId: 101,
            SourceVersionNumber: 4,
            TargetItemId: 202,
            ExpectedTargetVersionNumber: 8,
            SourceCulture: "en-US",
            TargetCulture: "fr-FR",
            TranslatedFields: new Dictionary<string, JsonElement>(),
            ProviderId: "provider",
            Model: "model");

        command.SourceItemId.ShouldBe(101);
        command.SourceVersionNumber.ShouldBe(4);
        command.TargetItemId.ShouldBe(202);
        command.ExpectedTargetVersionNumber.ShouldBe(8);
    }

    [Test]
    public void Translation_review_binds_approved_content_to_source_and_target_versions()
    {
        var review = ContentTranslationReview.Approve(
            sourceItemId: 101,
            sourceVersionNumber: 4,
            targetVersionNumber: 8,
            reviewedOn: DateTimeOffset.UnixEpoch,
            reviewedBy: "reviewer");

        review.ReviewedSourceItemId.ShouldBe(101);
        review.ReviewedSourceVersionNumber.ShouldBe(4);
        review.ReviewedTargetVersionNumber.ShouldBe(8);
    }

    [Test]
    public void Unbound_approved_reviews_cannot_be_constructed_or_deserialized_as_valid()
    {
        Should.Throw<ArgumentException>(() => new ContentTranslationReview(ContentTranslationReviewStatus.Approved));
        Should.Throw<Exception>(() => JsonSerializer.Deserialize(
            "{\"status\":\"approved\"}",
            ContentJsonContext.Default.ContentTranslationReview));
    }

    [Test]
    public void Localization_enums_reject_numeric_json_values()
    {
        Should.Throw<Exception>(() => JsonSerializer.Deserialize(
            "{\"status\":2}",
            ContentJsonContext.Default.ContentTranslationReview));
        Should.Throw<Exception>(() => JsonSerializer.Deserialize(
            "{\"origin\":2,\"sourceCulture\":\"en-US\",\"sourceVersionNumber\":1,\"createdOn\":\"1970-01-01T00:00:00+00:00\"}",
            ContentJsonContext.Default.ContentTranslationProvenance));
    }

    [Test]
    public void Explicit_null_localization_contract_values_normalize_to_safe_defaults()
    {
        var definition = JsonSerializer.Deserialize(
            "{\"localization\":null}",
            ContentJsonContext.Default.ContentTypeDefinition)!;
        var group = JsonSerializer.Deserialize(
            "{\"sharedFields\":null}",
            ContentJsonContext.Default.ContentTranslationGroup)!;
        var contentItemType = typeof(ContentTypeDefinition).Assembly.GetType(
            "Aero.Cms.Abstractions.Content.ContentItem",
            throwOnError: true)!;
        var item = JsonSerializer.Deserialize(
            "{\"translationReview\":null}",
            contentItemType,
            ContentJsonContext.Default.Options)!;
        var review = contentItemType.GetProperty("TranslationReview")!.GetValue(item);

        definition.Localization.ShouldNotBeNull();
        definition.Localization.CultureFallbackPolicy.ShouldBe(ContentCultureFallbackPolicy.ExactOnly);
        definition.Localization.AiTranslationReviewPolicy.ShouldBe(ContentAiTranslationReviewPolicy.RequireHumanReview);
        group.SharedFields.ShouldNotBeNull();
        group.SharedFields.ShouldBeEmpty();
        review.ShouldBeOfType<ContentTranslationReview>().Status.ShouldBe(ContentTranslationReviewStatus.NotRequired);
    }
}
