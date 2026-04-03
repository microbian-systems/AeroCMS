namespace Aero.Cms.Abstractions.Models;


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
}


[GenerateSerializer]
[Alias("AliasErrorViewModel")]
public record AliasErrorViewModel : AeroErrorViewModel<AliasViewModel>;
