using Aero.Cms.Abstractions.Enums;
using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor;

/// <summary>
/// Represents a class for PageEditorHeader.
/// </summary>
public sealed partial class PageEditorHeader : ComponentBase
{
        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
[Parameter] public string Title { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Last Saved.
    /// </summary>
[Parameter] public string LastSaved { get; set; } = "Never";
        /// <summary>
    /// Gets or sets the Author.
    /// </summary>
[Parameter] public string Author { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Is Saving.
    /// </summary>
[Parameter] public bool IsSaving { get; set; }
        /// <summary>
    /// Gets or sets the Publication State.
    /// </summary>
[Parameter] public ContentPublicationState PublicationState { get; set; } = ContentPublicationState.Draft;

        /// <summary>
    /// Gets or sets the On Title Changed.
    /// </summary>
[Parameter] public EventCallback<string> OnTitleChanged { get; set; }
        /// <summary>
    /// Gets or sets the On Toggle Preview.
    /// </summary>
[Parameter] public EventCallback OnTogglePreview { get; set; }
        /// <summary>
    /// Gets or sets the On Save.
    /// </summary>
[Parameter] public EventCallback OnSave { get; set; }
        /// <summary>
    /// Gets or sets the On Publish.
    /// </summary>
[Parameter] public EventCallback OnPublish { get; set; }
        /// <summary>
    /// Gets or sets the On Unpublish.
    /// </summary>
[Parameter] public EventCallback OnUnpublish { get; set; }
}
