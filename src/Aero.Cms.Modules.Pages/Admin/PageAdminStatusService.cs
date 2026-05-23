using Aero.Cms.Abstractions.Enums;

namespace Aero.Cms.Modules.Pages.Admin;

/// <summary>
/// Derives admin UI status from published document and editor state.
/// Version comparison lives here — not on <see cref="PageDocument"/>
/// or <see cref="PageEditorState"/>.
/// </summary>
public sealed class PageAdminStatusService
{
    /// <summary>
    /// Determines the admin UI status by comparing the published document
    /// against the current editor (draft) state.
    /// </summary>
    /// <param name="page">The published/lifecycle page document.</param>
    /// <param name="editor">
    /// The editor state document. When <c>null</c>, the page has no draft
    /// workspace and is treated as having no unpublished changes.
    /// </param>
    public PageAdminStatus GetStatus(PageDocument page, PageEditorState? editor)
    {
        return page.PublicationState switch
        {
            ContentPublicationState.Draft when page.PublishedVersion == 0
                => PageAdminStatus.NeverPublished,

            ContentPublicationState.Published when editor is not null
                && editor.DraftVersion > page.PublishedVersion
                => PageAdminStatus.PublishedWithDraftChanges,

            ContentPublicationState.Published
                => PageAdminStatus.Published,

            ContentPublicationState.Archived
                => PageAdminStatus.Archived,

            _ => PageAdminStatus.Draft
        };
    }
}

/// <summary>
/// Admin UI page status derived from published version comparison.
/// </summary>
public enum PageAdminStatus
{
    /// <summary>Never published — no published version exists.</summary>
    NeverPublished,

    /// <summary>Draft state with no pending review.</summary>
    Draft,

    /// <summary>Currently published with no unpublished draft changes.</summary>
    Published,

    /// <summary>Published, but the editor has unsaved draft changes.</summary>
    PublishedWithDraftChanges,

    /// <summary>The page is archived.</summary>
    Archived,

    /// <summary>Reserved — add when ScheduledPublishOn is introduced.</summary>
    Scheduled
}
