using System.Text.Json.Serialization;

namespace Aero.Cms.Abstractions.Blocks.Neo.Styles;

public sealed record BackgroundImageStyle
{
    public bool Enabled { get; init; } = true;

    public long MediaId { get; init; }

    public string Url { get; init; } = string.Empty;

    public BackgroundImageSize Size { get; init; } = BackgroundImageSize.Cover;

    public BackgroundImageRepeat Repeat { get; init; } =
        BackgroundImageRepeat.NoRepeat;

    public BackgroundImagePosition Position { get; init; } =
        BackgroundImagePosition.Center;
}

[JsonConverter(typeof(JsonStringEnumConverter<BackgroundImageSize>))]
public enum BackgroundImageSize
{
    Cover,
    Contain
}

[JsonConverter(typeof(JsonStringEnumConverter<BackgroundImageRepeat>))]
public enum BackgroundImageRepeat
{
    NoRepeat,
    Repeat,
    RepeatX,
    RepeatY
}

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
