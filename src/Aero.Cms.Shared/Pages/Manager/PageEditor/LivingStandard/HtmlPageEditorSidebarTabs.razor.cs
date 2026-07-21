using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.LivingStandard;

/// <summary>
/// Renders the page editor's right-sidebar tab list.
/// </summary>
public partial class HtmlPageEditorSidebarTabs
{
    private static readonly IReadOnlyList<SidebarTabOption> Tabs =
    [
        new(HtmlPageEditorSidebarTab.Document, "Document", "☷"),
        new(HtmlPageEditorSidebarTab.Elements, "Elements", "+"),
        new(HtmlPageEditorSidebarTab.Content, "Content", "◇"),
        new(HtmlPageEditorSidebarTab.Inspector, "Inspector", "⚙")
    ];

    /// <summary>Gets or sets the active sidebar tab.</summary>
    [Parameter]
    public HtmlPageEditorSidebarTab ActiveTab { get; set; }

    /// <summary>Raised when the author selects a sidebar tab.</summary>
    [Parameter]
    public EventCallback<HtmlPageEditorSidebarTab> ActiveTabChanged { get; set; }

    private static string TabId(HtmlPageEditorSidebarTab tab) =>
        $"aero-page-sidebar-tab-{tab.ToString().ToLowerInvariant()}";

    private Task SelectAsync(HtmlPageEditorSidebarTab tab) =>
        ActiveTabChanged.InvokeAsync(tab);

    private sealed record SidebarTabOption(
        HtmlPageEditorSidebarTab Value,
        string Label,
        string Icon);
}
