using Aero.Core.Railway;

namespace Aero.Cms.Html;

/// <summary>One stable, editor-facing component template descriptor.</summary>
public sealed record HtmlComponentDescriptor(
    string Key,
    string DisplayName,
    string Description,
    HtmlComponentCatalogGroup Group,
    string Icon,
    string RootTagName,
    IReadOnlyList<string> Keywords,
    Func<Result<HtmlNode>> Create,
    HtmlComponentPreview? Preview = null);

/// <summary>Optional local, compile-time preview metadata for a catalog composition.</summary>
public sealed record HtmlComponentPreview(
    string Category,
    string ThumbnailUrl,
    string AspectRatio = "16:9");

/// <summary>Bounded component catalog groups used by the PageEditor.</summary>
public enum HtmlComponentCatalogGroup
{
    Basics,
    Daisy,
    Patterns
}
