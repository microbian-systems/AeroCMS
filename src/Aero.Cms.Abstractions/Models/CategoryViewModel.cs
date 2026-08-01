namespace Aero.Cms.Abstractions.Models;

/// <summary>
/// Represents a record for CategoryViewModel.
/// </summary>
[GenerateSerializer]
public record CategoryViewModel : AeroEntityViewModel
{
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
[Id(0)]
    public string? Name { get; set; }
        /// <summary>
    /// Gets or sets the Slug.
    /// </summary>
[Id(1)]
    public string? Slug { get; set; }
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
[Id(2)]
    public string? Description { get; set; }
        /// <summary>
    /// Gets or sets the Parent Category Id.
    /// </summary>
[Id(3)]
    public long? ParentCategoryId { get; set; }
}

/// <summary>
/// Represents a record for CategoryErrorViewModel.
/// </summary>
[GenerateSerializer]
[Alias("CategoryErrorViewModel")]
public record CategoryErrorViewModel : AeroErrorViewModel<CategoryViewModel>;