namespace Aero.Cms.Shared.Pages.Manager.PageEditor;

/// <summary>
/// Tracks the palette catalog item currently being dragged through the page editor.
/// Nested composition surfaces consume this state when a palette item is dropped
/// directly into a container preview.
/// </summary>
public sealed class EditorPaletteDragState
{
    /// <summary>
    /// Raised when the active drag is cleared by a consumer.
    /// </summary>
    public event Action? Cleared;

    /// <summary>
    /// The catalog ID currently being dragged from the palette, if any.
    /// </summary>
    public string? CatalogId { get; private set; }

    /// <summary>
    /// Starts tracking a palette drag operation.
    /// </summary>
    public void Start(string catalogId) => CatalogId = catalogId;

    /// <summary>
    /// Clears the active palette drag operation.
    /// </summary>
    public void Clear()
    {
        CatalogId = null;
        Cleared?.Invoke();
    }

    /// <summary>
    /// Returns and clears the active catalog ID in one operation.
    /// </summary>
    public bool TryConsume(out string catalogId)
    {
        catalogId = CatalogId ?? string.Empty;
        if (string.IsNullOrWhiteSpace(catalogId))
        {
            return false;
        }

        Clear();
        return true;
    }
}
