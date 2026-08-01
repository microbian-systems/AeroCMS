namespace Aero.Cms.Shared.Pages.Manager.PageEditor;

/// <summary>An active toast notification.</summary>
public class ToastMessage
{
        /// <summary>
    /// Gets or sets the Id.
    /// </summary>
public string Id      { get; init; } = Guid.NewGuid().ToString();
        /// <summary>
    /// Gets or sets the Message.
    /// </summary>
public string Message { get; set; }  = string.Empty;
        /// <summary>
    /// Gets or sets the Type.
    /// </summary>
public string Type    { get; set; }  = "info"; // "success" | "error" | "info"
}