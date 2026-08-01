namespace Aero.Cms.Html;

/// <summary>
/// Framework-neutral surface intent for backgrounds and rounded containers.
/// </summary>
public sealed class CssSurfaceStyle
{
    /// <summary>Gets or sets the surface's solid background color.</summary>
    public CssColor? BackgroundColor { get; set; }
    /// <summary>Gets or sets a background image URL subject to the shared media URL policy.</summary>
    public string? BackgroundImageUrl { get; set; }
    /// <summary>Gets or sets the color blended over the background image.</summary>
    public CssColor? OverlayColor { get; set; }
    /// <summary>Gets or sets overlay opacity from zero through one.</summary>
    public decimal? OverlayOpacity { get; set; }
    /// <summary>Gets or sets how the background image fits the surface.</summary>
    public CssBackgroundFit? BackgroundFit { get; set; }
    /// <summary>Gets or sets the background image anchor.</summary>
    public CssBackgroundPosition? BackgroundPosition { get; set; }
    /// <summary>Gets or sets the background image repetition mode.</summary>
    public CssBackgroundRepeat? BackgroundRepeat { get; set; }
    /// <summary>Gets or sets a uniform corner radius.</summary>
    public CssLength? BorderRadius { get; set; }
}

/// <summary>Controls scaling of a surface background image.</summary>
public enum CssBackgroundFit
{
    /// <summary>Fills the surface while preserving aspect ratio, clipping overflow as needed.</summary>
    Cover,
    /// <summary>Fits the complete image inside the surface while preserving aspect ratio.</summary>
    Contain
}

/// <summary>Defines the supported background-image anchors.</summary>
public enum CssBackgroundPosition
{
    /// <summary>Centers the image.</summary>
    Center,
    /// <summary>Anchors the image at the block start.</summary>
    Top,
    /// <summary>Anchors the image at the block end.</summary>
    Bottom
}

/// <summary>Defines the supported background-image repetition modes.</summary>
public enum CssBackgroundRepeat
{
    /// <summary>Does not tile the image.</summary>
    NoRepeat,
    /// <summary>Tiles the image on both axes.</summary>
    Repeat,
    /// <summary>Tiles the image on the inline axis.</summary>
    RepeatX,
    /// <summary>Tiles the image on the block axis.</summary>
    RepeatY
}
