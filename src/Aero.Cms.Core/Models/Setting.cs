using Aero.Core.Data;
using AeroDB.Sable;

namespace Aero.Cms.Core.Models;

/// <summary>
/// Represents a system or application setting.
/// </summary>
public class Setting : SableDocument<string>, IAuditable
{
    /// <summary>
    /// Gets or sets the setting key (the unique identifier).
    /// </summary>
    public string Key { get => Id; set => Id = value; }

    /// <summary>
    /// Gets or sets the setting value.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the category of the setting.
    /// </summary>
    public string Category { get; set; } = "General";

    /// <summary>
    /// Gets or sets the optional description of the setting.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the value type (e.g., "string", "int", "bool", "json").
    /// </summary>
    public string Type { get; set; } = "string";

    // IAuditable
    /// <summary>Gets or sets the audit creation timestamp.</summary>
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>Gets or sets the most recent audit modification timestamp.</summary>
    public DateTimeOffset? ModifiedOn { get; set; }
    /// <summary>Gets or sets the identity that created this setting, if recorded.</summary>
    public string? CreatedBy { get; set; }
    /// <summary>Gets or sets the identity that last modified this setting, if recorded.</summary>
    public string? ModifiedBy { get; set; }
}
