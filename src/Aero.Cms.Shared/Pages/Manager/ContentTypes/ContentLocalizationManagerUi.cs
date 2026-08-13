using System.Globalization;
using Aero.Cms.Abstractions.Content.Localization;
using Aero.Cms.Shared.Localization;

namespace Aero.Cms.Shared.Pages.Manager.ContentTypes;

/// <summary>
/// Pure presentation decisions shared by the generic content localization manager.
/// </summary>
public static class ContentLocalizationManagerUi
{
    /// <summary>
    /// Existing types lock field localization when entries exist or the manager cannot prove
    /// the type is empty. Moving values between item and group ownership requires migration.
    /// </summary>
    public static bool ShouldLockFieldLocalization(bool isExistingType, long? itemCount) =>
        isExistingType && itemCount is null or > 0;

    /// <summary>Requires all persisted optimistic-concurrency tokens for a group mutation.</summary>
    public static bool HasExactLocalizationTokens(
        long itemStorageVersion,
        long? groupStorageVersion,
        int? groupRevision) =>
        itemStorageVersion > 0
        && groupStorageVersion is > 0
        && groupRevision is >= 0;

    /// <summary>Builds the culture-prefixed public route used by an embedded content preview.</summary>
    public static string BuildCultureAwareContentPath(
        string alias,
        string slug,
        string culture) =>
        AeroCultureRoute.BuildCulturePath(
            culture,
            $"{Uri.EscapeDataString(alias.Trim())}/{Uri.EscapeDataString(slug.Trim())}");

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
        int? currentSourceVersionNumber,
        int targetVersionNumber,
        bool isDirty)
    {
        if (!metadataAvailable)
        {
            return provenance?.Origin == ContentTranslationOrigin.AiAssisted
                ? new(false, false, "Review metadata unavailable", "Reload current revision and review metadata before publishing this AI-assisted translation.")
                : new(true, false, "Review metadata unavailable", "Publication remains subject to server-side policy.");
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

        if (isDirty)
        {
            return new(false, true, "Save and re-review required", "Save the translation changes, then have the new target revision reviewed before publication.");
        }

        if (sourceItemId is not > 0
            || currentSourceVersionNumber is not > 0
            || review.ReviewedSourceItemId != sourceItemId
            || review.ReviewedSourceVersionNumber != currentSourceVersionNumber
            || provenance.SourceVersionNumber != currentSourceVersionNumber)
        {
            return new(false, true, "Source approval is stale", "The source changed after translation or review. Refresh the translation and approve it again.");
        }

        if (targetVersionNumber <= 0
            || review.ReviewedTargetVersionNumber != targetVersionNumber)
        {
            return new(false, true, "Target approval is stale", "The translated variant changed after review and must be approved again.");
        }

        return new(true, true, "Approved", "Human approval matches the current source and target revisions.");
    }
}

/// <summary>Presentation-only publication decision for the manager.</summary>
public sealed record ContentTranslationPublishDecision(
    bool CanPublish,
    bool MetadataAvailable,
    string Label,
    string Detail);
