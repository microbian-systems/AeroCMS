namespace Aero.Cms.Abstractions.Blocks.Embed;

/// <summary>
/// Standard aspect ratios for embedded content.
/// </summary>
public enum AspectRatio
{
    /// <summary>16:9 widescreen — default for video embeds.</summary>
    Widescreen,

    /// <summary>4:3 standard — maps, presentations.</summary>
    Standard,

    /// <summary>1:1 square — social media posts.</summary>
    Square,

    /// <summary>21:9 ultrawide — cinematic content.</summary>
    Ultrawide,
}

/// <summary>
/// Extension methods for <see cref="AspectRatio"/>.
/// </summary>
public static class AspectRatioExtensions
{
    /// <summary>
    /// Returns the CSS padding-top percentage for the aspect-ratio container technique.
    /// e.g., Widescreen (16:9) returns "56.25%".
    /// </summary>
    public static string ToCssPercent(this AspectRatio ratio) => ratio switch
    {
        AspectRatio.Widescreen => "56.25%",
        AspectRatio.Standard => "75%",
        AspectRatio.Square => "100%",
        AspectRatio.Ultrawide => "42.857%",
        _ => "56.25%"
    };
}
