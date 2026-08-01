namespace Aero.Cms.Shared.Pages.Manager.PageEditor.LivingStandard;

/// <summary>
/// Identifies whether the Markdown interchange dialog accepts Markdown for conversion or
/// presents previously exported Markdown.
/// </summary>
public enum MarkdownInterchangeMode
{
    /// <summary>
    /// Accepts Markdown that the caller will validate and insert into the page.
    /// </summary>
    Import,

    /// <summary>
    /// Presents a read-only Markdown representation supplied by the caller.
    /// </summary>
    Export
}
