namespace Aero.Cms.Abstractions.Models;

/// <summary>
/// Contains the exact editable source snapshot returned only by the manager
/// authoring boundary.
/// </summary>
/// <param name="VersionId">The append-only source-version identifier.</param>
/// <param name="RendererId">The renderer that owns the source.</param>
/// <param name="SourceHash">The lowercase hexadecimal SHA-256 source hash.</param>
/// <param name="Source">The exact persisted source text.</param>
[GenerateSerializer]
[Alias("PageSourceViewModel")]
public sealed record PageSourceViewModel(
    [property: Id(0)] long VersionId,
    [property: Id(1)] string RendererId,
    [property: Id(2)] string SourceHash,
    [property: Id(3)] string Source);
