namespace Aero.Cms.Shared.Pages.Manager.PageEditor;

/// <summary>A simplified nested block (text / image / video / button) inside a column.</summary>
public class NestedBlock
{
    public string Type    { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Src     { get; set; } = string.Empty;
    public string Alt     { get; set; } = string.Empty;
    public string Url     { get; set; } = string.Empty;
    public string Text    { get; set; } = string.Empty;
    public string Style   { get; set; } = "primary";

    public NestedBlock Clone() => (NestedBlock)MemberwiseClone();
}