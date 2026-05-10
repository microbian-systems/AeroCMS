using System.Text.Json;

namespace Aero.Cms.Abstractions.Content;

public interface IContentFieldIndexer
{
    string FieldType { get; }

    IEnumerable<string> GetIndexTokens(ContentFieldDefinition field, JsonElement value);
}
