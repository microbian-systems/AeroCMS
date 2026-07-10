namespace Aero.Cms.Shared.Pages.Manager.PageEditor.Catalog;

/// <summary>
/// Represents a record for NeoPropertyDefinition.
/// </summary>
public sealed record NeoPropertyDefinition
{
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public required string Name { get; init; }
        /// <summary>
    /// Gets or sets the Label.
    /// </summary>
public required string Label { get; init; }
        /// <summary>
    /// Gets or sets the Field Type.
    /// </summary>
public required NeoPropertyFieldType FieldType { get; init; }
        /// <summary>
    /// Gets or sets the Required.
    /// </summary>
public bool Required { get; init; }
        /// <summary>
    /// Gets or sets the Default Value.
    /// </summary>
public string? DefaultValue { get; init; }
        /// <summary>
    /// Gets or sets the Options.
    /// </summary>
public IReadOnlyList<string> Options { get; init; } = [];
}
