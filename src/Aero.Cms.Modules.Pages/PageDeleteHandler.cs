using Aero.Cms.Core.Entities;
using Marten;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Pages;

/// <summary>
/// Handles page deletion cleanup by hard-deleting the corresponding
/// <see cref="PageEditorState"/> document. PageEditorState is editor scratch
/// state — it does not participate in soft delete and does not belong in any
/// audit trail.
/// </summary>
public sealed class PageDeleteHandler
{
    private readonly IDocumentSession _session;
    private readonly ILogger<PageDeleteHandler> _logger;

    public PageDeleteHandler(IDocumentSession session, ILogger<PageDeleteHandler> logger)
    {
        _session = session;
        _logger = logger;
    }

    /// <summary>
    /// Hard-deletes the <see cref="PageEditorState"/> for the given page ID.
    /// Called after a page is soft-deleted so the editor document is cleaned
    /// up immediately.
    /// </summary>
    /// <param name="pageId">The ID of the page being deleted.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task HandleAsync(long pageId, CancellationToken ct = default)
    {
        _logger.LogDebug("Hard-deleting PageEditorState for page {PageId}", pageId);

        _session.Delete<PageEditorState>(pageId);
        await _session.SaveChangesAsync(ct);

        _logger.LogInformation("PageEditorState cleaned up for deleted page {PageId}", pageId);
    }
}
