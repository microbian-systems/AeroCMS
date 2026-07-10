namespace Aero.Cms.Abstractions.Blocks;

/// <summary>A simplified nested block (text / image / video / button) inside a column.</summary>
public class NestedBlock
{
        /// <summary>
    /// Gets or sets the Id.
    /// </summary>
public string Id      { get; set; } = Guid.NewGuid().ToString("N");
        /// <summary>
    /// Gets or sets the Type.
    /// </summary>
public string Type    { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Content.
    /// </summary>
public string Content { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Src.
    /// </summary>
public string Src     { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Alt.
    /// </summary>
public string Alt     { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Url.
    /// </summary>
public string Url     { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Text.
    /// </summary>
public string Text    { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Style.
    /// </summary>
public string Style   { get; set; } = "primary";

        /// <summary>
    /// Clone method.
    /// </summary>
public NestedBlock Clone()
    {
        var clone = (NestedBlock)MemberwiseClone();
        clone.Id = Guid.NewGuid().ToString("N");
        return clone;
    }
}
