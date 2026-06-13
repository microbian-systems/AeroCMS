using Aero.Cms.Abstractions.Enums;
using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor;

public sealed partial class PageEditorHeader : ComponentBase
{
    [Parameter] public string Title { get; set; } = string.Empty;
    [Parameter] public string LastSaved { get; set; } = "Never";
    [Parameter] public string Author { get; set; } = string.Empty;
    [Parameter] public bool IsSaving { get; set; }
    [Parameter] public ContentPublicationState PublicationState { get; set; } = ContentPublicationState.Draft;

    [Parameter] public EventCallback<string> OnTitleChanged { get; set; }
    [Parameter] public EventCallback OnTogglePreview { get; set; }
    [Parameter] public EventCallback OnSave { get; set; }
    [Parameter] public EventCallback OnPublish { get; set; }
    [Parameter] public EventCallback OnUnpublish { get; set; }
}
