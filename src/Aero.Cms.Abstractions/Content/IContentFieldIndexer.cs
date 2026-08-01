namespace Aero.Cms.Abstractions.Content;

/// <summary>
/// Defines an interface for IContentFieldIndexer.
/// </summary>
public interface IContentFieldIndexer
{
        /// <summary>
    /// Gets or sets the Field Type.
    /// </summary>
string FieldType { get; }

        /// <summary>
    /// GetIndexTokens method.
    /// </summary>
IEnumerable<string> GetIndexTokens(ContentFieldDefinition field, JsonElement value);
}
