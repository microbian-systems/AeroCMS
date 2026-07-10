namespace Aero.Cms.Abstractions.Blocks.Neo.Styles;

/// <summary>
/// Resolved visual style for one node at one breakpoint.
/// </summary>
public sealed record NodeStyle
{
        /// <summary>
    /// Gets or sets the Margin.
    /// </summary>
public LogicalSpacing Margin { get; init; } = new();

        /// <summary>
    /// Gets or sets the Padding.
    /// </summary>
public LogicalSpacing Padding { get; init; } = new();

        /// <summary>
    /// Gets or sets the Width.
    /// </summary>
public CssLength? Width { get; init; }

        /// <summary>
    /// Gets or sets the Height.
    /// </summary>
public CssLength? Height { get; init; }

        /// <summary>
    /// Gets or sets the Minimum Width.
    /// </summary>
public CssLength? MinimumWidth { get; init; }

        /// <summary>
    /// Gets or sets the Maximum Width.
    /// </summary>
public CssLength? MaximumWidth { get; init; }

        /// <summary>
    /// Gets or sets the Minimum Height.
    /// </summary>
public CssLength? MinimumHeight { get; init; }

        /// <summary>
    /// Gets or sets the Maximum Height.
    /// </summary>
public CssLength? MaximumHeight { get; init; }

        /// <summary>
    /// Gets or sets the Opacity.
    /// </summary>
public decimal? Opacity { get; init; }

        /// <summary>
    /// Gets or sets the Foreground Color.
    /// </summary>
public CssColor? ForegroundColor { get; init; }

        /// <summary>
    /// Gets or sets the Background Color.
    /// </summary>
public CssColor? BackgroundColor { get; init; }

        /// <summary>
    /// Gets or sets the Background Overlay Color.
    /// </summary>
public CssColor? BackgroundOverlayColor { get; init; }

        /// <summary>
    /// Gets or sets the Background Gradient.
    /// </summary>
public LinearGradient? BackgroundGradient { get; init; }

        /// <summary>
    /// Gets or sets the Background Image.
    /// </summary>
public BackgroundImageStyle? BackgroundImage { get; init; }

        /// <summary>
    /// Gets or sets the Background Video.
    /// </summary>
public BackgroundVideoStyle? BackgroundVideo { get; init; }

        /// <summary>
    /// Gets or sets the Border Color.
    /// </summary>
public CssColor? BorderColor { get; init; }

        /// <summary>
    /// Gets or sets the Border Width.
    /// </summary>
public CssLength? BorderWidth { get; init; }

        /// <summary>
    /// Gets or sets the Border Radius.
    /// </summary>
public CssLength? BorderRadius { get; init; }

        /// <summary>
    /// Gets or sets the Shadow.
    /// </summary>
public BoxShadow? Shadow { get; init; }

        /// <summary>
    /// Gets or sets the Hidden.
    /// </summary>
public bool Hidden { get; init; }

        /// <summary>
    /// Gets or sets the Font Size.
    /// </summary>
public CssLength? FontSize { get; init; }

        /// <summary>
    /// Gets or sets the Font Weight.
    /// </summary>
public FontWeight FontWeight { get; init; } = FontWeight.Inherit;

        /// <summary>
    /// Gets or sets the Line Height.
    /// </summary>
public decimal? LineHeight { get; init; }

        /// <summary>
    /// Gets or sets the Letter Spacing.
    /// </summary>
public CssLength? LetterSpacing { get; init; }

        /// <summary>
    /// Gets or sets the Text Alignment.
    /// </summary>
public TextAlignment TextAlignment { get; init; } = TextAlignment.Inherit;

        /// <summary>
    /// Gets or sets the Horizontal Content Alignment.
    /// </summary>
public HorizontalContentAlignment HorizontalContentAlignment { get; init; } =
        HorizontalContentAlignment.Inherit;

        /// <summary>
    /// Gets or sets the Vertical Content Alignment.
    /// </summary>
public VerticalContentAlignment VerticalContentAlignment { get; init; } =
        VerticalContentAlignment.Inherit;

        /// <summary>
    /// Gets or sets the Direction.
    /// </summary>
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
            BackgroundVideo = value.BackgroundVideo ?? BackgroundVideo,
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
            HorizontalContentAlignment =
                value.HorizontalContentAlignment ?? HorizontalContentAlignment,
            VerticalContentAlignment =
                value.VerticalContentAlignment ?? VerticalContentAlignment,
            Direction = value.Direction ?? Direction
        };
    }
}

/// <summary>
/// Optional values that override an inherited <see cref="NodeStyle"/>.
/// </summary>
public sealed record NodeStyleOverride
{
        /// <summary>
    /// Gets or sets the Margin.
    /// </summary>
public LogicalSpacingOverride? Margin { get; init; }

        /// <summary>
    /// Gets or sets the Padding.
    /// </summary>
public LogicalSpacingOverride? Padding { get; init; }

        /// <summary>
    /// Gets or sets the Width.
    /// </summary>
public CssLength? Width { get; init; }

        /// <summary>
    /// Gets or sets the Height.
    /// </summary>
public CssLength? Height { get; init; }

        /// <summary>
    /// Gets or sets the Minimum Width.
    /// </summary>
public CssLength? MinimumWidth { get; init; }

        /// <summary>
    /// Gets or sets the Maximum Width.
    /// </summary>
public CssLength? MaximumWidth { get; init; }

        /// <summary>
    /// Gets or sets the Minimum Height.
    /// </summary>
public CssLength? MinimumHeight { get; init; }

        /// <summary>
    /// Gets or sets the Maximum Height.
    /// </summary>
public CssLength? MaximumHeight { get; init; }

        /// <summary>
    /// Gets or sets the Opacity.
    /// </summary>
public decimal? Opacity { get; init; }

        /// <summary>
    /// Gets or sets the Foreground Color.
    /// </summary>
public CssColor? ForegroundColor { get; init; }

        /// <summary>
    /// Gets or sets the Background Color.
    /// </summary>
public CssColor? BackgroundColor { get; init; }

        /// <summary>
    /// Gets or sets the Background Overlay Color.
    /// </summary>
public CssColor? BackgroundOverlayColor { get; init; }

        /// <summary>
    /// Gets or sets the Background Gradient.
    /// </summary>
public LinearGradient? BackgroundGradient { get; init; }

        /// <summary>
    /// Gets or sets the Background Image.
    /// </summary>
public BackgroundImageStyle? BackgroundImage { get; init; }

        /// <summary>
    /// Gets or sets the Background Video.
    /// </summary>
public BackgroundVideoStyle? BackgroundVideo { get; init; }

        /// <summary>
    /// Gets or sets the Border Color.
    /// </summary>
public CssColor? BorderColor { get; init; }

        /// <summary>
    /// Gets or sets the Border Width.
    /// </summary>
public CssLength? BorderWidth { get; init; }

        /// <summary>
    /// Gets or sets the Border Radius.
    /// </summary>
public CssLength? BorderRadius { get; init; }

        /// <summary>
    /// Gets or sets the Shadow.
    /// </summary>
public BoxShadow? Shadow { get; init; }

        /// <summary>
    /// Gets or sets the Hidden.
    /// </summary>
public bool? Hidden { get; init; }

        /// <summary>
    /// Gets or sets the Font Size.
    /// </summary>
public CssLength? FontSize { get; init; }

        /// <summary>
    /// Gets or sets the Font Weight.
    /// </summary>
public FontWeight? FontWeight { get; init; }

        /// <summary>
    /// Gets or sets the Line Height.
    /// </summary>
public decimal? LineHeight { get; init; }

        /// <summary>
    /// Gets or sets the Letter Spacing.
    /// </summary>
public CssLength? LetterSpacing { get; init; }

        /// <summary>
    /// Gets or sets the Text Alignment.
    /// </summary>
public TextAlignment? TextAlignment { get; init; }

        /// <summary>
    /// Gets or sets the Horizontal Content Alignment.
    /// </summary>
public HorizontalContentAlignment? HorizontalContentAlignment { get; init; }

        /// <summary>
    /// Gets or sets the Vertical Content Alignment.
    /// </summary>
public VerticalContentAlignment? VerticalContentAlignment { get; init; }

        /// <summary>
    /// Gets or sets the Direction.
    /// </summary>
public ContentDirection? Direction { get; init; }
}
