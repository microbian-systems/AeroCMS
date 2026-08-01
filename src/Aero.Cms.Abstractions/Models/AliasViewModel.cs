namespace Aero.Cms.Abstractions.Models;


/// <summary>
/// Represents a record for AliasViewModel.
/// </summary>
[Alias("AliasViewModel")]
[GenerateSerializer]
public record AliasViewModel : AeroEntityViewModel
{
    /// <summary>
    /// Gets or sets the original file or directory path before a rename or move operation.
    /// </summary>
    [Id(0)]
    public string? OldPath { get; set; }

    /// <summary>
    /// Gets or sets the new file or directory path to be used in the operation.
    /// </summary>
    [Id(1)]
    public string? NewPath { get; set; }

    /// <summary>
    /// Gets or sets optional notes or comments associated with the object.
    /// </summary>
    [Id(2)]
    public string? Notes { get; set; }

    /// <summary>Gets or sets the route culture.</summary>
    [Id(3)]
    public string Culture { get; set; } = "en-US";

    /// <summary>Gets or sets the redirect status code.</summary>
    [Id(4)]
    public int StatusCode { get; set; } = 301;

    /// <summary>Gets whether this alias is managed by a content route.</summary>
    [Id(5)]
    public bool IsAutomatic { get; set; }
}


/// <summary>
/// Represents a record for AliasErrorViewModel.
/// </summary>
[GenerateSerializer]
[Alias("AliasErrorViewModel")]
public record AliasErrorViewModel : AeroErrorViewModel<AliasViewModel>;
