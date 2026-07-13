namespace Aero.Cms.Html;

/// <summary>
/// Framework-neutral surface intent for backgrounds and rounded containers.
/// </summary>
public sealed class CssSurfaceStyle
{
    public CssColor? BackgroundColor { get; set; }
    public string? BackgroundImageUrl { get; set; }
    public CssColor? OverlayColor { get; set; }
    public decimal? OverlayOpacity { get; set; }
    public CssBackgroundFit? BackgroundFit { get; set; }
    public CssBackgroundPosition? BackgroundPosition { get; set; }
    public CssBackgroundRepeat? BackgroundRepeat { get; set; }
    public CssLength? BorderRadius { get; set; }
}

public enum CssBackgroundFit
{
    Cover,
    Contain
}

public enum CssBackgroundPosition
{
    Center,
    Top,
    Bottom
}

public enum CssBackgroundRepeat
{
    NoRepeat,
    Repeat,
    RepeatX,
    RepeatY
}
