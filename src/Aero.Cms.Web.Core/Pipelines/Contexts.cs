namespace Aero.Cms.Web.Core.Pipelines;


using Aero.Cms.Core.Pipelines;

/// <summary>
/// Carries caller-supplied route dimensions and mutable state through a page-read pipeline.
/// </summary>
/// <remarks>
/// The context does not normalize routes, resolve tenants, authorize draft access, or constrain metadata values.
/// Hooks share the same mutable instance; concurrent use is not supported by this type.
/// </remarks>
public class PageReadContext : PipelineContext
{
        /// <summary>
    /// Gets the required route slug supplied by the caller.
    /// </summary>
public required string Slug { get; init; }
        /// <summary>
    /// Gets the required culture label supplied by the caller.
    /// </summary>
public required string Culture { get; init; }
        /// <summary>
    /// Gets the optional tenant identifier used as a pipeline input; isolation is enforced elsewhere.
    /// </summary>
public long? TenantId { get; init; }
        /// <summary>
    /// Gets whether the caller requested draft-inclusive loading; this flag is not an authorization decision.
    /// </summary>
public bool IncludeDraft { get; init; }
        /// <summary>
    /// Gets or sets the untyped page value shared by read hooks.
    /// </summary>
public object? Page { get; set; } // TODO: Replace with Page model when defined
        /// <summary>
    /// Gets the mutable per-execution metadata dictionary shared by hooks.
    /// </summary>
public Dictionary<string, object> Metadata { get; } = new();
}

/// <summary>
/// Carries an untyped page, operation label, and accumulated validation messages through a save pipeline.
/// </summary>
/// <remarks>This context does not persist, validate, authorize, or establish transaction ownership by itself.</remarks>
public class PageSaveContext : PipelineContext
{
        /// <summary>
    /// Gets or sets the required untyped page value being processed.
    /// </summary>
public required object Page { get; set; } // TODO: Replace with Page model when defined
        /// <summary>
    /// Gets the required caller-supplied operation label; values are not validated.
    /// </summary>
public required string Operation { get; init; } // TODO: Replace with enum
        /// <summary>
    /// Gets the mutable list to which pipeline participants may append validation messages.
    /// </summary>
public List<string> ValidationErrors { get; } = [];
        /// <summary>
    /// Gets whether at least one validation message is currently present.
    /// </summary>
public bool HasValidationErrors => ValidationErrors.Count > 0;
}
