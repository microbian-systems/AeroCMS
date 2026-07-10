namespace Aero.Cms.Abstractions.Content;

/// <summary>
/// Represents a class for FieldBlockInstance.
/// </summary>
public sealed class FieldBlockInstance
{
        /// <summary>
    /// Gets or sets the Id.
    /// </summary>
public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>The field name from ContentTypeDefinition.Fields</summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>The component alias to render this field</summary>
    public string ComponentAlias { get; set; } = string.Empty;

    /// <summary>Optional overrides passed to the block renderer</summary>
    public Dictionary<string, JsonElement> Props { get; set; } = [];
}
