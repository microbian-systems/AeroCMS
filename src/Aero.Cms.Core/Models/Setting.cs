using Aero.Core.Entities;

namespace Aero.Cms.Core;

/// <summary>
/// Represents a system or application setting.
/// </summary>
public class Setting : Entity<string>
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
}
