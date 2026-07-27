using Aero.Cms.Abstractions.Ai.Pipeline;

namespace Aero.Cms.Abstractions.Ai.Knowledge;

/// <summary>
/// Classifies how a field may be used when Aero assembles AI retrieval context.
/// </summary>
public enum AeroAiFieldExposure
{
    /// <summary>The field may be used by public, member, and manager retrieval.</summary>
    Public = 0,

    /// <summary>The field may be used only by an authorized manager workflow.</summary>
    Internal = 1,

    /// <summary>The field is excluded unless a separate, narrowly scoped workflow explicitly permits it.</summary>
    Sensitive = 2,

    /// <summary>The field must never be indexed, embedded, placed in a prompt, or returned by an AI tool.</summary>
    Secret = 3
}

/// <summary>
/// Explicit record-level controls used before content is admitted to public AI retrieval.
/// </summary>
public sealed record AeroAiContentExposure(
    bool IncludeInSearch = true,
    bool IncludeInPublicAi = false);

/// <summary>
/// Fail-closed rules shared by indexing, retrieval, and tests.
/// </summary>
public static class AeroAiContentExposureRules
{
    /// <summary>
    /// Returns whether one published record may enter the public AI corpus.
    /// Search inclusion alone is intentionally insufficient.
    /// </summary>
    public static bool IsEligibleForPublicAi(
        bool isPublished,
        bool includeInSearch,
        bool includeInPublicAi)
        => isPublished && includeInSearch && includeInPublicAi;

    /// <summary>
    /// Returns whether a field classification is available to the requested audience.
    /// Sensitive and secret fields require a separate explicit workflow and are denied here.
    /// </summary>
    public static bool IsFieldAvailable(
        AeroAiAudience audience,
        AeroAiFieldExposure exposure)
        => exposure switch
        {
            AeroAiFieldExposure.Public => true,
            AeroAiFieldExposure.Internal => audience == AeroAiAudience.Manager,
            AeroAiFieldExposure.Sensitive => false,
            AeroAiFieldExposure.Secret => false,
            _ => false
        };
}
