namespace Aero.Cms.Abstractions.Models;

/// <summary>
/// Represents a record for TagViewModel.
/// </summary>
[Alias("TagViewModel")]
[GenerateSerializer]
public record TagViewModel : AeroEntityViewModel
{
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
[Id(0)] public string? Name { get; set; }
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
[Id(1)] public string? Description { get; set; }
        /// <summary>
    /// Gets or sets the Slug.
    /// </summary>
[Id(2)] public string? Slug { get; set; }
}

/// <summary>
/// Represents a record for TagErrorViewModel.
/// </summary>
[GenerateSerializer]
[Alias("TagErrorViewModel")]
public record TagErrorViewModel : AeroErrorViewModel<TagViewModel>;