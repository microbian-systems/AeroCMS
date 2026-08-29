using System.Text.Json;
using Aero.Cms.Abstractions.Content.Localization;
using Aero.Cms.Abstractions.Http.Clients;
using Shouldly;

namespace Aero.Cms.Shared.Tests.Localization;

public sealed class ContentLocalizationHttpRequestTests
{
    [Test]
    public void Ai_apply_and_review_requests_carry_all_exact_storage_tokens()
    {
        var fields = new Dictionary<string, JsonElement>
        {
            ["title"] = JsonSerializer.SerializeToElement("Bonjour")
        };
        var apply = new ApplyContentItemAiTranslationRequest(
            202, 4, 8, "en-US", "fr-FR", fields, "provider", "model", 17, 19, 23);
        var review = new ReviewContentItemTranslationRequest(
            202, 4, 9, true, "Reviewed", 17, 29, 31);

        apply.ExpectedSourceStorageVersion.ShouldBe(17);
        apply.ExpectedTargetStorageVersion.ShouldBe(19);
        apply.ExpectedGroupStorageVersion.ShouldBe(23);
        review.ExpectedSourceStorageVersion.ShouldBe(17);
        review.ExpectedTargetStorageVersion.ShouldBe(29);
        review.ExpectedGroupStorageVersion.ShouldBe(31);
    }

    [Test]
    public void Shared_update_and_fork_requests_bind_group_cas_tokens()
    {
        var fields = new Dictionary<string, JsonElement>
        {
            ["species"] = JsonSerializer.SerializeToElement("view:species:abc")
        };
        var shared = new UpdateContentItemTranslationSharedFieldsRequest(55, 12, 3, fields);
        var fork = new ForkContentItemCultureRequest("fr-CA", "loup", 12, 19);

        shared.TranslationGroupId.ShouldBe(55);
        shared.ExpectedGroupStorageVersion.ShouldBe(12);
        shared.ExpectedGroupRevision.ShouldBe(3);
        shared.SharedFields.Keys.ShouldBe(["species"]);
        fork.ExpectedGroupStorageVersion.ShouldBe(12);
        fork.ExpectedTargetStorageVersion.ShouldBe(19);
    }

    [Test]
    public void Content_type_detail_policy_defaults_fail_closed_when_omitted()
    {
        var settings = new ContentLocalizationSettings();

        settings.CultureFallbackPolicy.ShouldBe(ContentCultureFallbackPolicy.ExactOnly);
        settings.AiTranslationReviewPolicy.ShouldBe(ContentAiTranslationReviewPolicy.RequireHumanReview);
    }
}
