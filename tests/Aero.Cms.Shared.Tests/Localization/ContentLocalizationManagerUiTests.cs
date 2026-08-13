using Aero.Cms.Abstractions.Content.Localization;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Shared.Pages.Manager.ContentTypes;
using Shouldly;

namespace Aero.Cms.Shared.Tests.Localization;

public sealed class ContentLocalizationManagerUiTests
{
    [Test]
    public void Localization_mutations_require_exact_nonzero_storage_and_group_tokens()
    {
        ContentLocalizationManagerUi.HasExactLocalizationTokens(7, 11, 0).ShouldBeTrue();
        ContentLocalizationManagerUi.HasExactLocalizationTokens(0, 11, 0).ShouldBeFalse();
        ContentLocalizationManagerUi.HasExactLocalizationTokens(7, null, 0).ShouldBeFalse();
        ContentLocalizationManagerUi.HasExactLocalizationTokens(7, 11, null).ShouldBeFalse();
    }

    [Test]
    public void Ai_assisted_content_with_unavailable_revision_metadata_cannot_publish()
    {
        var provenance = new ContentTranslationProvenance(
            ContentTranslationOrigin.AiAssisted,
            "en-US",
            4,
            DateTimeOffset.UnixEpoch);

        var decision = ContentLocalizationManagerUi.EvaluatePublishDecision(
            false,
            provenance,
            ContentTranslationReview.Pending(),
            ContentAiTranslationReviewPolicy.RequireHumanReview,
            101,
            null,
            8,
            false);

        decision.CanPublish.ShouldBeFalse();
        decision.Label.ShouldBe("Review metadata unavailable");
    }

    [Test]
    public void Culture_aware_preview_path_prefixes_the_variant_culture()
    {
        var path = ContentLocalizationManagerUi.BuildCultureAwareContentPath(
            "animal profile",
            "river otter",
            "fr-CA");

        path.ShouldBe("/fr-CA/animal%20profile/river%20otter");
    }

    [Test]
    public void Preview_direction_uses_the_culture_writing_direction()
    {
        ContentLocalizationManagerUi.ResolveTextDirection("ar-SA").ShouldBe("rtl");
        ContentLocalizationManagerUi.ResolveTextDirection("en-US").ShouldBe("ltr");
        ContentLocalizationManagerUi.ResolveTextDirection("not-a-culture").ShouldBe("ltr");
    }

    [Test]
    public void Existing_type_with_entries_locks_field_localization()
    {
        ContentLocalizationManagerUi.ShouldLockFieldLocalization(true, 3).ShouldBeTrue();
        ContentLocalizationManagerUi.ShouldLockFieldLocalization(true, 0).ShouldBeFalse();
    }

    [Test]
    public void Existing_type_with_unknown_entry_count_fails_closed()
    {
        ContentLocalizationManagerUi.ShouldLockFieldLocalization(true, null).ShouldBeTrue();
        ContentLocalizationManagerUi.ShouldLockFieldLocalization(false, null).ShouldBeFalse();
    }

    [Test]
    public void Localized_current_variant_forks_from_authoritative_canonical_source_with_exact_tokens()
    {
        var sourceReference = ContentItem(
            id: 101,
            groupId: 55,
            sourceItemId: null,
            storageVersion: 0,
            groupStorageVersion: null);
        var authoritativeSource = sourceReference with
        {
            StorageVersion = 19,
            TranslationGroupStorageVersion = 23
        };

        var preparation = ContentTranslationForkUi.Prepare(
            currentItemId: 202,
            currentTranslationGroupId: 55,
            sourceReference,
            authoritativeSource,
            culture: "ar-SA",
            slug: "ثعلب");

        preparation.CanFork.ShouldBeTrue();
        preparation.SourceItemId.ShouldBe(101);
        preparation.Request.ShouldNotBeNull();
        preparation.Request.Culture.ShouldBe("ar-SA");
        preparation.Request.Slug.ShouldBe("ثعلب");
        preparation.Request.ExpectedGroupStorageVersion.ShouldBe(23);
        preparation.Request.ExpectedSourceStorageVersion.ShouldBe(19);
        preparation.Request.ExpectedTargetStorageVersion.ShouldBeNull();
    }

    [Test]
    [Arguments(false, false, 19, 23)]
    [Arguments(true, true, 19, 23)]
    [Arguments(true, false, 0, 23)]
    [Arguments(true, false, 19, 0)]
    public void Missing_cross_group_or_zero_token_source_fails_closed(
        bool includeSource,
        bool useDifferentGroup,
        long storageVersion,
        long groupStorageVersion)
    {
        var sourceReference = includeSource
            ? ContentItem(101, 55, null, 0, null)
            : null;
        var authoritativeSource = includeSource
            ? ContentItem(
                101,
                useDifferentGroup ? 77 : 55,
                null,
                storageVersion,
                groupStorageVersion)
            : null;

        var preparation = ContentTranslationForkUi.Prepare(
            currentItemId: 202,
            currentTranslationGroupId: 55,
            sourceReference,
            authoritativeSource,
            culture: "ar-SA",
            slug: "ثعلب");

        preparation.CanFork.ShouldBeFalse();
        preparation.SourceItemId.ShouldBe(0);
        preparation.Request.ShouldBeNull();
        preparation.ReloadMessage.ShouldContain("Reload translations");
    }

    [Test]
    public void Approved_clean_translation_can_publish()
    {
        var provenance = new ContentTranslationProvenance(
            ContentTranslationOrigin.AiAssisted,
            "en-US",
            4,
            DateTimeOffset.UnixEpoch,
            "provider",
            "model");
        var matchingReview = ContentTranslationReview.Approve(
            sourceItemId: 101,
            sourceVersionNumber: 4,
            targetVersionNumber: 8,
            reviewedOn: DateTimeOffset.UnixEpoch,
            reviewedBy: "reviewer");

        var approved = ContentLocalizationManagerUi.EvaluatePublishDecision(
            true,
            provenance,
            matchingReview,
            ContentAiTranslationReviewPolicy.RequireHumanReview,
            101,
            4,
            8,
            isDirty: false);

        approved.CanPublish.ShouldBeTrue();
        approved.Label.ShouldBe("Approved");
    }

    [Test]
    public void Dirty_approved_translation_requires_save_and_re_review()
    {
        var (provenance, review) = ApprovedTranslation();
        var dirty = ContentLocalizationManagerUi.EvaluatePublishDecision(
            true,
            provenance,
            review,
            ContentAiTranslationReviewPolicy.RequireHumanReview,
            101,
            4,
            8,
            isDirty: true);

        dirty.CanPublish.ShouldBeFalse();
        dirty.Label.ShouldBe("Save and re-review required");
    }

    [Test]
    public void Changed_source_makes_approval_stale()
    {
        var (provenance, review) = ApprovedTranslation();
        var stale = ContentLocalizationManagerUi.EvaluatePublishDecision(
            true,
            provenance,
            review,
            ContentAiTranslationReviewPolicy.RequireHumanReview,
            101,
            currentSourceVersionNumber: 5,
            targetVersionNumber: 8,
            isDirty: false);

        stale.CanPublish.ShouldBeFalse();
        stale.Label.ShouldBe("Source approval is stale");
    }

    [Test]
    public void Changed_target_makes_approval_stale()
    {
        var (provenance, review) = ApprovedTranslation();
        var stale = ContentLocalizationManagerUi.EvaluatePublishDecision(
            true,
            provenance,
            review,
            ContentAiTranslationReviewPolicy.RequireHumanReview,
            101,
            currentSourceVersionNumber: 4,
            targetVersionNumber: 9,
            isDirty: false);

        stale.CanPublish.ShouldBeFalse();
        stale.Label.ShouldBe("Target approval is stale");
    }

    [Test]
    public void Missing_manager_metadata_is_explicit_without_claiming_review_failure()
    {
        var decision = ContentLocalizationManagerUi.EvaluatePublishDecision(
            false,
            null,
            null,
            ContentAiTranslationReviewPolicy.RequireHumanReview,
            null,
            null,
            0,
            isDirty: false);

        decision.MetadataAvailable.ShouldBeFalse();
        decision.CanPublish.ShouldBeTrue();
        decision.Label.ShouldBe("Review metadata unavailable");
    }

    private static (ContentTranslationProvenance Provenance, ContentTranslationReview Review) ApprovedTranslation()
    {
        var provenance = new ContentTranslationProvenance(
            ContentTranslationOrigin.AiAssisted,
            "en-US",
            4,
            DateTimeOffset.UnixEpoch,
            "provider",
            "model");
        var review = ContentTranslationReview.Approve(
            sourceItemId: 101,
            sourceVersionNumber: 4,
            targetVersionNumber: 8,
            reviewedOn: DateTimeOffset.UnixEpoch,
            reviewedBy: "reviewer");
        return (provenance, review);
    }

    private static ContentItemDetail ContentItem(
        long id,
        long? groupId,
        long? sourceItemId,
        long storageVersion,
        long? groupStorageVersion) =>
        new(
            id,
            $"Item {id}",
            $"item-{id}",
            "animal",
            new Dictionary<string, System.Text.Json.JsonElement>(),
            "Draft",
            null,
            1,
            null,
            null,
            "en-US",
            groupId,
            sourceItemId,
            StorageVersion: storageVersion,
            TranslationGroupStorageVersion: groupStorageVersion);
}
