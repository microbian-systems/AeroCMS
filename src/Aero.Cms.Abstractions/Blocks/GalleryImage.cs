namespace Aero.Cms.Abstractions.Blocks;

/// <summary>A single image in a gallery block.</summary>
public class GalleryImage
{
    public string Src { get; set; } = string.Empty;
    public string Alt { get; set; } = string.Empty;
}