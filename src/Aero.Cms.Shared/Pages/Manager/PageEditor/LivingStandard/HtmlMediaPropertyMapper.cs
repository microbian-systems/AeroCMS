using Aero.Cms.Html;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.LivingStandard;

/// <summary>
/// Maps a selected media-library item onto a copy of the selected node's editable properties.
/// </summary>
public static class HtmlMediaPropertyMapper
{
    public static HtmlNodeProperties Map(
        HtmlNode node,
        HtmlMediaTargetKind target,
        string source,
        string? alternativeText = null)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        var properties = HtmlNodeProperties.From(node);
        switch (target)
        {
            case HtmlMediaTargetKind.ElementSource:
                properties.Attributes["src"] = source;
                if (node.TagName == "img"
                    && (!properties.Attributes.TryGetValue("alt", out var currentAlternativeText)
                        || string.IsNullOrWhiteSpace(currentAlternativeText))
                    && !string.IsNullOrWhiteSpace(alternativeText))
                {
                    properties.Attributes["alt"] = alternativeText;
                }
                break;
            case HtmlMediaTargetKind.ResponsiveSourceSet:
                properties.Attributes["srcset"] = source;
                break;
            case HtmlMediaTargetKind.VideoPoster:
                properties.Attributes["poster"] = source;
                break;
            case HtmlMediaTargetKind.BackgroundImage:
                properties.Style ??= new HtmlStyle();
                properties.Style.Surface ??= new CssSurfaceStyle();
                properties.Style.Surface.BackgroundImageUrl = source;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(target), target, null);
        }

        return properties;
    }
}
