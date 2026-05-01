namespace Aero.Cms.Abstractions.Blocks.Editing;

/// <summary>
/// Provides metadata about a block property for the admin UI.
/// </summary>
public sealed class BlockPropertyMetadata
{
    /// <summary>
    /// Gets the name of the property.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the display name shown in the admin UI.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the property type name for UI rendering (e.g., "string", "int", "bool").
    /// </summary>
    public string PropertyType { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the property is required.
    /// </summary>
    public bool IsRequired { get; init; }

    /// <summary>
    /// Gets the default value for the property.
    /// </summary>
    public string? DefaultValue { get; init; }

    /// <summary>
    /// Gets the available options for selection-based properties (e.g., dropdowns).
    /// </summary>
    public List<string>? Options { get; init; }
}
