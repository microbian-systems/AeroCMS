namespace Aero.Cms.Abstractions.Models;

/// <summary>
/// Represents a record for SeriesViewModel.
/// </summary>
[Alias("SeriesViewModel")]
[GenerateSerializer]
public record SeriesViewModel : AeroEntityViewModel
{
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
[Id(0)] public string? Name { get; set; }
        /// <summary>
    /// Gets or sets the Slug.
    /// </summary>
[Id(1)] public string? Slug { get; set; }
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
[Id(2)] public string? Description { get; set; }
}

/// <summary>
/// Represents a record for SeriesErrorViewModel.
/// </summary>
[GenerateSerializer]
[Alias("SeriesErrorViewModel")]
public record SeriesErrorViewModel : AeroErrorViewModel<SeriesViewModel>;
