using System.Text.Json;
using System.Text.Json.Serialization;
using Aero.Core.Railway;
using Aero.Core;

namespace Aero.Cms.Abstractions.Content.Localization;

/// <summary>Controls how a field value is represented in a localized content variant.</summary>
[JsonConverter(typeof(StrictCamelCaseEnumConverter<ContentFieldLocalizationMode>))]
public enum ContentFieldLocalizationMode
{
    /// <summary>The field is shared by all culture variants in its translation group.</summary>
    Shared = 0,

    /// <summary>Each culture variant supplies an independent field value.</summary>
    Localized = 1,

    /// <summary>The source value is copied when a variant is forked, then becomes independent.</summary>
    CopyOnFork = 2
}

/// <summary>Controls whether a culture lookup may resolve a non-exact variant.</summary>
[JsonConverter(typeof(StrictCamelCaseEnumConverter<ContentCultureFallbackPolicy>))]
public enum ContentCultureFallbackPolicy
{
    /// <summary>Only a variant for the requested culture is eligible.</summary>
    ExactOnly = 0,

    /// <summary>Try the parent culture, then the site's default culture, after an exact lookup.</summary>
    ParentCultureThenDefaultCulture = 1
}

/// <summary>Controls review requirements for AI-assisted translations.</summary>
[JsonConverter(typeof(StrictCamelCaseEnumConverter<ContentAiTranslationReviewPolicy>))]
public enum ContentAiTranslationReviewPolicy
{
    /// <summary>AI-assisted translations require a human approval before publication.</summary>
    RequireHumanReview = 0,

    /// <summary>An implementation may publish AI-assisted translations without human approval.</summary>
    AllowUnreviewedPublication = 1
}

/// <summary>Identifies how a localized variant was created.</summary>
[JsonConverter(typeof(StrictCamelCaseEnumConverter<ContentTranslationOrigin>))]
public enum ContentTranslationOrigin
{
    /// <summary>The variant was authored directly by an editor.</summary>
    Manual = 0,

    /// <summary>The variant was created by copying another culture variant.</summary>
    Fork = 1,

    /// <summary>The variant was created or changed with AI assistance.</summary>
    AiAssisted = 2,

    /// <summary>The variant was supplied by an import process.</summary>
    Imported = 3
}

/// <summary>Represents the editorial review state of a translated variant.</summary>
[JsonConverter(typeof(StrictCamelCaseEnumConverter<ContentTranslationReviewStatus>))]
public enum ContentTranslationReviewStatus
{
    /// <summary>No translation-specific review is required.</summary>
    NotRequired = 0,

    /// <summary>Review is required before publication.</summary>
    Pending = 1,

    /// <summary>A reviewer approved the translation.</summary>
    Approved = 2,

    /// <summary>A reviewer rejected the translation.</summary>
    Rejected = 3
}

/// <summary>Serializes localization enum values as camel-case strings and rejects numeric input.</summary>
public sealed class StrictCamelCaseEnumConverter<TEnum> : JsonStringEnumConverter<TEnum>
    where TEnum : struct, Enum
{
    /// <summary>Initializes the converter with fail-closed numeric handling.</summary>
    public StrictCamelCaseEnumConverter()
        : base(JsonNamingPolicy.CamelCase, allowIntegerValues: false)
    {
    }
}

/// <summary>Content-type-level localization behavior. Defaults are deliberately fail closed.</summary>
public sealed class ContentLocalizationSettings
{
    /// <summary>Gets or sets the lookup behavior when an exact culture variant is absent.</summary>
    public ContentCultureFallbackPolicy CultureFallbackPolicy { get; set; } = ContentCultureFallbackPolicy.ExactOnly;

    /// <summary>Gets or sets the review behavior for AI-assisted translations.</summary>
    public ContentAiTranslationReviewPolicy AiTranslationReviewPolicy { get; set; } = ContentAiTranslationReviewPolicy.RequireHumanReview;
}

/// <summary>
/// Stable identity, source information, and the sole durable field bag for values shared by all
/// culture variants in a translation group.
/// </summary>
public sealed class ContentTranslationGroup
{
    private Dictionary<string, JsonElement> sharedFields = [];
    /// <summary>Gets or sets the persisted translation-group identifier.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the authoritative site identifier.</summary>
    public long SiteId { get; set; }

    /// <summary>Gets or sets the invariant content-type alias.</summary>
    public string ContentTypeAlias { get; set; } = string.Empty;

    /// <summary>Gets or sets the group source item identifier.</summary>
    public long SourceItemId { get; set; }

    /// <summary>Gets or sets the source item's culture.</summary>
    public string SourceCulture { get; set; } = string.Empty;

    /// <summary>Gets or sets when the translation group was created.</summary>
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the monotonically increasing optimistic-concurrency revision for this group.
    /// </summary>
    public int Revision { get; set; }

    /// <summary>
    /// Gets or sets values for fields whose <see cref="ContentFieldLocalizationMode"/> is shared.
    /// Content items must not durably store these values.
    /// </summary>
    public Dictionary<string, JsonElement> SharedFields
    {
        get => sharedFields;
        set => sharedFields = value ?? [];
    }
}

/// <summary>Records the non-content source details used to create a localized variant.</summary>
public sealed record ContentTranslationProvenance(
    ContentTranslationOrigin Origin,
    string SourceCulture,
    int SourceVersionNumber,
    DateTimeOffset CreatedOn,
    string? ProviderId = null,
    string? Model = null);

/// <summary>Records a translation-specific editorial review decision and its exact revision basis.</summary>
public sealed class ContentTranslationReview
{
    /// <summary>Creates a review state that requires no translation-specific approval.</summary>
    public ContentTranslationReview()
    {
    }

    /// <summary>
    /// Creates a review state. Approved and rejected states require the exact source and target
    /// revisions that were reviewed; unbound decisions are rejected at the contract boundary.
    /// </summary>
    [JsonConstructor]
    public ContentTranslationReview(
        ContentTranslationReviewStatus status,
        DateTimeOffset? reviewedOn = null,
        string? reviewedBy = null,
        string? notes = null,
        long? reviewedSourceItemId = null,
        int? reviewedSourceVersionNumber = null,
        int? reviewedTargetVersionNumber = null)
    {
        if (status is ContentTranslationReviewStatus.Approved or ContentTranslationReviewStatus.Rejected
            && (reviewedSourceItemId is null or <= 0
                || reviewedSourceVersionNumber is null or <= 0
                || reviewedTargetVersionNumber is null or <= 0))
        {
            throw new ArgumentException(
                "Approved and rejected translation reviews require source item, source version, and target version bindings.",
                nameof(reviewedSourceItemId));
        }

        Status = status;
        ReviewedOn = reviewedOn;
        ReviewedBy = reviewedBy;
        Notes = notes;
        ReviewedSourceItemId = reviewedSourceItemId;
        ReviewedSourceVersionNumber = reviewedSourceVersionNumber;
        ReviewedTargetVersionNumber = reviewedTargetVersionNumber;
    }

    /// <summary>Gets the current review status.</summary>
    public ContentTranslationReviewStatus Status { get; } = ContentTranslationReviewStatus.NotRequired;

    /// <summary>Gets when the review decision was made.</summary>
    public DateTimeOffset? ReviewedOn { get; }

    /// <summary>Gets the identity of the reviewer, when available.</summary>
    public string? ReviewedBy { get; }

    /// <summary>Gets optional reviewer notes.</summary>
    public string? Notes { get; }

    /// <summary>Gets the source item reviewed as the translation basis.</summary>
    public long? ReviewedSourceItemId { get; }

    /// <summary>Gets the source version reviewed as the translation basis.</summary>
    public int? ReviewedSourceVersionNumber { get; }

    /// <summary>Gets the target variant version that was reviewed.</summary>
    public int? ReviewedTargetVersionNumber { get; }

    /// <summary>Creates a pending review state.</summary>
    public static ContentTranslationReview Pending(string? notes = null) =>
        new(ContentTranslationReviewStatus.Pending, notes: notes);

    /// <summary>Creates an approved review state with mandatory revision bindings.</summary>
    public static ContentTranslationReview Approve(
        long sourceItemId,
        int sourceVersionNumber,
        int targetVersionNumber,
        DateTimeOffset reviewedOn,
        string? reviewedBy = null,
        string? notes = null) =>
        new(ContentTranslationReviewStatus.Approved, reviewedOn, reviewedBy, notes, sourceItemId, sourceVersionNumber, targetVersionNumber);

    /// <summary>Creates a rejected review state with mandatory revision bindings.</summary>
    public static ContentTranslationReview Reject(
        long sourceItemId,
        int sourceVersionNumber,
        int targetVersionNumber,
        DateTimeOffset reviewedOn,
        string? reviewedBy = null,
        string? notes = null) =>
        new(ContentTranslationReviewStatus.Rejected, reviewedOn, reviewedBy, notes, sourceItemId, sourceVersionNumber, targetVersionNumber);
}

/// <summary>Authoritative site and culture inputs supplied to content-localization operations.</summary>
public sealed record ContentLocalizationContext(
    long SiteId,
    string DefaultCulture,
    IReadOnlyList<string> SupportedCultures,
    ContentCultureFallbackPolicy CultureFallbackPolicy);

/// <summary>Requests an independent culture variant copied from an existing content item.</summary>
public sealed record ContentCultureForkCommand(
    long SourceItemId,
    string TargetCulture,
    string TargetSlug,
    bool OverwriteExisting = false,
    long? ExpectedGroupStorageVersion = null);

/// <summary>Applies bounded AI-translated field values to a target culture variant.</summary>
public sealed record ApplyContentAiTranslationCommand(
    long SourceItemId,
    int SourceVersionNumber,
    long TargetItemId,
    int ExpectedTargetVersionNumber,
    string SourceCulture,
    string TargetCulture,
    IReadOnlyDictionary<string, JsonElement> TranslatedFields,
    string ProviderId,
    string Model,
    long? ExpectedSourceStorageVersion = null,
    long? ExpectedTargetStorageVersion = null,
    long? ExpectedGroupStorageVersion = null);

/// <summary>Records a human review decision against the exact current source and target revisions.</summary>
public sealed record ReviewContentTranslationCommand(
    long SourceItemId,
    int SourceVersionNumber,
    long TargetItemId,
    int TargetVersionNumber,
    bool Approved,
    string? Notes = null,
    long? ExpectedSourceStorageVersion = null,
    long? ExpectedTargetStorageVersion = null,
    long? ExpectedGroupStorageVersion = null);

/// <summary>Changes group-owned shared fields under both storage and semantic revision fences.</summary>
public sealed record UpdateContentTranslationSharedFieldsCommand(
    long TranslationGroupId,
    long ExpectedGroupStorageVersion,
    int ExpectedGroupRevision,
    IReadOnlyDictionary<string, JsonElement> SharedFields);

/// <summary>Reports the persisted item and group affected by a localization operation.</summary>
public sealed record ContentLocalizationOperationResult(
    long ContentItemId,
    long TranslationGroupId,
    string Culture,
    ContentTranslationReviewStatus ReviewStatus,
    long ContentItemStorageVersion,
    long TranslationGroupStorageVersion,
    int TranslationGroupRevision);

/// <summary>Executes site-scoped localization operations without exposing storage implementation details.</summary>
public interface IContentLocalizationHandler
{
    /// <summary>Creates or replaces a culture fork according to the supplied context.</summary>
    Task<Result<ContentLocalizationOperationResult, AeroError>> ForkAsync(
        ContentLocalizationContext context,
        ContentCultureForkCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>Applies AI-translated values and establishes the required review state.</summary>
    Task<Result<ContentLocalizationOperationResult, AeroError>> ApplyAiTranslationAsync(
        ContentLocalizationContext context,
        ApplyContentAiTranslationCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>Records a revision-bound human review for an AI-assisted variant.</summary>
    Task<Result<ContentLocalizationOperationResult, AeroError>> ReviewAsync(
        ContentLocalizationContext context,
        ReviewContentTranslationCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<ContentLocalizationOperationResult, AeroError>> UpdateSharedFieldsAsync(
        ContentLocalizationContext context,
        UpdateContentTranslationSharedFieldsCommand command,
        CancellationToken cancellationToken = default);
}
