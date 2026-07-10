using System.Text.Json.Serialization;

namespace Aero.Cms.Abstractions.Blocks.Neo.Styles;

/// <summary>
/// Represents a record for BackgroundImageStyle.
/// </summary>
public sealed record BackgroundImageStyle
{
        /// <summary>
    /// Gets or sets the Enabled.
    /// </summary>
public bool Enabled { get; init; } = true;

        /// <summary>
    /// Gets or sets the Media Id.
    /// </summary>
public long MediaId { get; init; }

        /// <summary>
    /// Gets or sets the Url.
    /// </summary>
public string Url { get; init; } = string.Empty;

        /// <summary>
    /// Gets or sets the Size.
    /// </summary>
public BackgroundImageSize Size { get; init; } = BackgroundImageSize.Cover;

        /// <summary>
    /// Gets or sets the Repeat.
    /// </summary>
public BackgroundImageRepeat Repeat { get; init; } =
        BackgroundImageRepeat.NoRepeat;

        /// <summary>
    /// Gets or sets the Position.
    /// </summary>
public BackgroundImagePosition Position { get; init; } =
        BackgroundImagePosition.Center;
}

/// <summary>
/// Defines an enumeration for BackgroundImageSize.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<BackgroundImageSize>))]
public enum BackgroundImageSize
{
    Cover,
    Contain
}

/// <summary>
/// Defines an enumeration for BackgroundImageRepeat.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<BackgroundImageRepeat>))]
public enum BackgroundImageRepeat
{
    NoRepeat,
    Repeat,
    RepeatX,
    RepeatY
}

/// <summary>
/// Defines an enumeration for BackgroundImagePosition.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<BackgroundImagePosition>))]
public enum BackgroundImagePosition
{
    BlockStartInlineStart,
    BlockStartCenter,
    BlockStartInlineEnd,
    CenterInlineStart,
    Center,
    CenterInlineEnd,
    BlockEndInlineStart,
    BlockEndCenter,
    BlockEndInlineEnd
}
