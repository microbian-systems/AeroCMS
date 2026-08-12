using Aero.Cms.Abstractions.Content.Localization;
using Aero.Cms.Shared.Pages.Manager.ContentTypes;
using Shouldly;

namespace Aero.Cms.Shared.Tests.Localization;

public sealed class ContentLocalizationManagerUiTests
{
    [Test]
    public void Culture_aware_preview_path_prefixes_the_variant_culture()
    {
        var path = ContentLocalizationManagerUi.BuildCultureAwareContentPath(
            "animal profile",
            "river otter",
            "fr-CA");

        path.ShouldBe("/fr-ca/content/animal%20profile/river%20otter");
    }

    [Test]
    public void Preview_direction_uses_the_culture_writing_direction()
    {
        ContentLocalizationManagerUi.ResolveTextDirection("ar-SA").ShouldBe("rtl");
        ContentLocalizationManagerUi.ResolveTextDirection("en-US").ShouldBe("ltr");
        ContentLocalizationManagerUi.ResolveTextDirection("not-a-culture").ShouldBe("ltr");
    }

    [Test]
    public void Ai_assisted_translation_requires_revision_bound_approval()
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
            8);
        var stale = ContentLocalizationManagerUi.EvaluatePublishDecision(
            true,
            provenance,
            matchingReview,
            ContentAiTranslationReviewPolicy.RequireHumanReview,
            101,
            9);

        approved.CanPublish.ShouldBeTrue();
        stale.CanPublish.ShouldBeFalse();
        stale.Label.ShouldBe("Approval is stale");
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
            0);

        decision.MetadataAvailable.ShouldBeFalse();
        decision.CanPublish.ShouldBeTrue();
        decision.Label.ShouldBe("Review metadata unavailable");
    }
}
