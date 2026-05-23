namespace Aero.Cms.Abstractions.Content;

public sealed class FieldBlockInstance
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>The field name from ContentTypeDefinition.Fields</summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>The component alias to render this field</summary>
    public string ComponentAlias { get; set; } = string.Empty;

    /// <summary>Optional overrides passed to the block renderer</summary>
    public Dictionary<string, JsonElement> Props { get; set; } = [];
}
