namespace Aero.Cms.Abstractions.Models;

public record AliasViewModel : EntityViewModel
{
    /// <summary>
    /// Gets or sets the original file or directory path before a rename or move operation.
    /// </summary>
    public string OldPath { get; set; } = null!;
    /// <summary>
    /// Gets or sets the new file or directory path to be used in the operation.
    /// </summary>
    public string NewPath { get; set; } = null!;
    /// <summary>
    /// Gets or sets optional notes or comments associated with the object.
    /// </summary>
    public string? Notes { get; set; } = null!;
}
