using Aero.Cms.Abstractions.Interfaces;
using AeroDB.Sable;

namespace Aero.Cms.Core.Entities;

/// <summary>
/// Stores one append-only, exact source snapshot for a source-rendered page.
/// </summary>
/// <remarks>
/// The Pages module is the sole writer. Source text is persisted without trimming,
/// newline conversion, or any other normalization.
/// </remarks>
public sealed class PageSourceVersion : SableDocument, ISiteOwned
{
    /// <summary>Gets the site that owns the source version.</summary>
    public required long SiteId { get; set; }

    /// <summary>Gets the page that owns the source version.</summary>
    public required long PageId { get; init; }

    /// <summary>Gets the stable renderer identifier associated with the source.</summary>
    public required string RendererId { get; init; }

    /// <summary>Gets the exact persisted source text.</summary>
    public required string Source { get; init; }

    /// <summary>Gets the lowercase hexadecimal SHA-256 hash of <see cref="Source"/>.</summary>
    public required string SourceHash { get; init; }

    /// <summary>Gets the timestamp at which this version was staged.</summary>
    public required DateTimeOffset CreatedOn { get; init; }

    /// <summary>Gets the actor that staged this version, when available.</summary>
    public string? CreatedBy { get; init; }
}
