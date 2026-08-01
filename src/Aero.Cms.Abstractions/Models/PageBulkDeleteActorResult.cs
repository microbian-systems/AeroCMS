namespace Aero.Cms.Abstractions.Models;

/// <summary>
/// Serializable outcome for a site-scoped page bulk deletion.
/// </summary>
[Alias("PageBulkDeleteActorResult")]
[GenerateSerializer]
public sealed record PageBulkDeleteActorResult
{
    /// <summary>Gets the number of distinct pages deleted.</summary>
    [Id(0)]
    public int Deleted { get; init; }

    /// <summary>Gets whether at least one requested page was absent from the authorized site.</summary>
    [Id(1)]
    public bool NotFound { get; init; }

    /// <summary>Gets an operation error when deletion failed for another reason.</summary>
    [Id(2)]
    public string? Error { get; init; }
}
