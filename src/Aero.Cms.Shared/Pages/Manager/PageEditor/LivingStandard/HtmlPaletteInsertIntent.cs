using Aero.Cms.Html;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.LivingStandard;

public enum HtmlPaletteItemKind
{
    Element,
    Layout,
    Component,
    ContentList,
    ContentItem,
    ContentField,
    RenderedFragment,
    RegisteredFragment
}

/// <summary>
/// Requests insertion of one palette template relative to a stable canvas node.
/// </summary>
public sealed record HtmlPaletteInsertIntent(
    HtmlPaletteItemKind ItemKind,
    string ItemValue,
    long TargetNodeId,
    HtmlRelativePlacement Placement);
