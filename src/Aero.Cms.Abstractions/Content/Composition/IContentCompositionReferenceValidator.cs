using Aero.Cms.Abstractions.Pages.Composition;
using Aero.Cms.Abstractions.Content.Views;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Abstractions.Content.Composition;

/// <summary>
/// Selects the lifecycle boundary applied while validating page-to-content references.
/// </summary>
public enum ContentReferenceValidationMode
{
    /// <summary>Validate existence and schema compatibility while editing a draft.</summary>
    Authoring,

    /// <summary>Also require explicitly selected content items to be published.</summary>
    Publishing
}

/// <summary>
/// Validates site-scoped content-type, item, query-field, and binding-field references.
/// </summary>
/// <remarks>
/// The Content module implements this contract. Pages supplies its composition sidecar but
/// remains responsible for HTML node ownership and structural validation.
/// </remarks>
public interface IContentCompositionReferenceValidator
{
    /// <summary>
    /// Validates the content references in one page composition snapshot.
    /// </summary>
    /// <param name="siteId">The site that owns both the page and referenced content.</param>
    /// <param name="culture">The page culture used for slug-based item lookup.</param>
    /// <param name="composition">The structurally valid page composition sidecar.</param>
    /// <param name="mode">The authoring or publishing lifecycle boundary.</param>
    /// <param name="ct">A token that can cancel content lookups.</param>
    /// <returns>A successful result when every referenced definition still exists.</returns>
    Task<Result<bool, AeroError>> ValidateAsync(
        long siteId,
        string culture,
        PageCompositionDocument composition,
        ContentReferenceValidationMode mode,
        CancellationToken ct = default);

    /// <summary>Validates references with an explicit server-authoritative tenant/site scope.</summary>
    Task<Result<bool, AeroError>> ValidateAsync(
        ContentViewScope scope,
        string culture,
        PageCompositionDocument composition,
        ContentReferenceValidationMode mode,
        CancellationToken ct = default);
}
