using System.Globalization;
using Aero.Cms.Abstractions.Content.Localization;
using Aero.Cms.Shared.Localization;

namespace Aero.Cms.Shared.Pages.Manager.ContentTypes;

/// <summary>
/// Pure presentation decisions shared by the generic content localization manager.
/// </summary>
public static class ContentLocalizationManagerUi
{
    /// <summary>Builds the culture-prefixed public route used by an embedded content preview.</summary>
    public static string BuildCultureAwareContentPath(
        string alias,
        string slug,
        string culture) =>
        AeroCultureRoute.BuildCulturePath(
            culture,
            $"content/{Uri.EscapeDataString(alias.Trim())}/{Uri.EscapeDataString(slug.Trim())}");

    /// <summary>Returns the writing direction for a valid culture, defaulting safely to LTR.</summary>
    public static string ResolveTextDirection(string? culture)
    {
        try
        {
            return !string.IsNullOrWhiteSpace(culture)
                   && CultureInfo.GetCultureInfo(culture.Trim()).TextInfo.IsRightToLeft
                ? "rtl"
                : "ltr";
        }
        catch (CultureNotFoundException)
        {
            return "ltr";
        }
    }

    /// <summary>
    /// Evaluates the client-side publication affordance for an AI-assisted translation.
    /// The server remains authoritative; missing metadata is surfaced as unavailable rather
    /// than treated as proof that a review is required.
    /// </summary>
    public static ContentTranslationPublishDecision EvaluatePublishDecision(
        bool metadataAvailable,
        ContentTranslationProvenance? provenance,
        ContentTranslationReview? review,
        ContentAiTranslationReviewPolicy reviewPolicy,
        long? sourceItemId,
        int targetVersionNumber)
    {
        if (!metadataAvailable)
        {
            return new(true, false, "Review metadata unavailable", "Publication remains subject to server-side policy.");
        }

        if (provenance?.Origin != ContentTranslationOrigin.AiAssisted)
        {
            return new(true, true, "Human-authored", "No AI translation review is required.");
        }

        if (reviewPolicy == ContentAiTranslationReviewPolicy.AllowUnreviewedPublication)
        {
            return new(true, true, "AI-assisted", "This content type allows unreviewed AI-assisted publication.");
        }

        if (review is null || review.Status != ContentTranslationReviewStatus.Approved)
        {
            return new(false, true, "Human review required", "A reviewer must approve this exact translation revision before publication.");
        }

        var approvalMatchesRevision = sourceItemId is > 0
                                      && review.ReviewedSourceItemId == sourceItemId
                                      && review.ReviewedSourceVersionNumber == provenance.SourceVersionNumber
                                      && targetVersionNumber > 0
                                      && review.ReviewedTargetVersionNumber == targetVersionNumber;

        return approvalMatchesRevision
            ? new(true, true, "Approved", "Human approval matches the current source and target revisions.")
            : new(false, true, "Approval is stale", "The translation changed after review and must be approved again.");
    }
}

/// <summary>Presentation-only publication decision for the manager.</summary>
public sealed record ContentTranslationPublishDecision(
    bool CanPublish,
    bool MetadataAvailable,
    string Label,
    string Detail);
