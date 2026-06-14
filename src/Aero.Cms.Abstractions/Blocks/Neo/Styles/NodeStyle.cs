namespace Aero.Cms.Abstractions.Blocks.Neo.Styles;

/// <summary>
/// Resolved visual style for one node at one breakpoint.
/// </summary>
public sealed record NodeStyle
{
    public LogicalSpacing Margin { get; init; } = new();

    public LogicalSpacing Padding { get; init; } = new();

    public CssLength? Width { get; init; }

    public CssLength? Height { get; init; }

    public CssLength? MinimumWidth { get; init; }

    public CssLength? MaximumWidth { get; init; }

    public CssLength? MinimumHeight { get; init; }

    public CssLength? MaximumHeight { get; init; }

    public decimal? Opacity { get; init; }

    public CssColor? ForegroundColor { get; init; }

    public CssColor? BackgroundColor { get; init; }

    public CssColor? BackgroundOverlayColor { get; init; }

    public LinearGradient? BackgroundGradient { get; init; }

    public BackgroundImageStyle? BackgroundImage { get; init; }

    public CssColor? BorderColor { get; init; }

    public CssLength? BorderWidth { get; init; }

    public CssLength? BorderRadius { get; init; }

    public BoxShadow? Shadow { get; init; }

    public bool Hidden { get; init; }

    public CssLength? FontSize { get; init; }

    public FontWeight FontWeight { get; init; } = FontWeight.Inherit;

    public decimal? LineHeight { get; init; }

    public CssLength? LetterSpacing { get; init; }

    public TextAlignment TextAlignment { get; init; } = TextAlignment.Inherit;

    public ContentDirection Direction { get; init; } = ContentDirection.Inherit;

    internal NodeStyle Apply(NodeStyleOverride? value)
    {
        if (value is null)
        {
            return this;
        }

        return this with
        {
            Margin = Margin.Apply(value.Margin),
            Padding = Padding.Apply(value.Padding),
            Width = value.Width ?? Width,
            Height = value.Height ?? Height,
            MinimumWidth = value.MinimumWidth ?? MinimumWidth,
            MaximumWidth = value.MaximumWidth ?? MaximumWidth,
            MinimumHeight = value.MinimumHeight ?? MinimumHeight,
            MaximumHeight = value.MaximumHeight ?? MaximumHeight,
            Opacity = value.Opacity ?? Opacity,
            ForegroundColor = value.ForegroundColor ?? ForegroundColor,
            BackgroundColor = value.BackgroundColor ?? BackgroundColor,
            BackgroundOverlayColor = value.BackgroundOverlayColor ?? BackgroundOverlayColor,
            BackgroundGradient = value.BackgroundGradient ?? BackgroundGradient,
            BackgroundImage = value.BackgroundImage ?? BackgroundImage,
            BorderColor = value.BorderColor ?? BorderColor,
            BorderWidth = value.BorderWidth ?? BorderWidth,
            BorderRadius = value.BorderRadius ?? BorderRadius,
            Shadow = value.Shadow ?? Shadow,
            Hidden = value.Hidden ?? Hidden,
            FontSize = value.FontSize ?? FontSize,
            FontWeight = value.FontWeight ?? FontWeight,
            LineHeight = value.LineHeight ?? LineHeight,
            LetterSpacing = value.LetterSpacing ?? LetterSpacing,
            TextAlignment = value.TextAlignment ?? TextAlignment,
            Direction = value.Direction ?? Direction
        };
    }
}

/// <summary>
/// Optional values that override an inherited <see cref="NodeStyle"/>.
/// </summary>
public sealed record NodeStyleOverride
{
    public LogicalSpacingOverride? Margin { get; init; }

    public LogicalSpacingOverride? Padding { get; init; }

    public CssLength? Width { get; init; }

    public CssLength? Height { get; init; }

    public CssLength? MinimumWidth { get; init; }

    public CssLength? MaximumWidth { get; init; }

    public CssLength? MinimumHeight { get; init; }

    public CssLength? MaximumHeight { get; init; }

    public decimal? Opacity { get; init; }

    public CssColor? ForegroundColor { get; init; }

    public CssColor? BackgroundColor { get; init; }

    public CssColor? BackgroundOverlayColor { get; init; }

    public LinearGradient? BackgroundGradient { get; init; }

    public BackgroundImageStyle? BackgroundImage { get; init; }

    public CssColor? BorderColor { get; init; }

    public CssLength? BorderWidth { get; init; }

    public CssLength? BorderRadius { get; init; }

    public BoxShadow? Shadow { get; init; }

    public bool? Hidden { get; init; }

    public CssLength? FontSize { get; init; }

    public FontWeight? FontWeight { get; init; }

    public decimal? LineHeight { get; init; }

    public CssLength? LetterSpacing { get; init; }

    public TextAlignment? TextAlignment { get; init; }

    public ContentDirection? Direction { get; init; }
}
