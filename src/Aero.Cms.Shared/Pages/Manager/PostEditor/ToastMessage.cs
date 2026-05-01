namespace Aero.Cms.Shared.Pages.Manager.PostEditor;

/// <summary>An active toast notification.</summary>
public class ToastMessage
{
    public string Id      { get; init; } = Guid.NewGuid().ToString();
    public string Message { get; set; }  = string.Empty;
    public string Type    { get; set; }  = "info"; // "success" | "error" | "info"
}
