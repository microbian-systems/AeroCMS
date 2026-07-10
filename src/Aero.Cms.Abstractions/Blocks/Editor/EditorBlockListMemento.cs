using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Serialization;

namespace Aero.Cms.Abstractions.Blocks.Editor;

/// <summary>
/// Represents a class for EditorBlockListMemento.
/// </summary>
public sealed class EditorBlockListMemento
{
    private readonly string _json;

    private EditorBlockListMemento(string json)
    {
        _json = json;
    }

        /// <summary>
    /// Capture method.
    /// </summary>
public static EditorBlockListMemento Capture(IReadOnlyList<EditorBlock> blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);

        return new EditorBlockListMemento(
            JsonSerializer.Serialize(blocks, BlockJsonContext.Default.IReadOnlyListEditorBlock));
    }

        /// <summary>
    /// Restore method.
    /// </summary>
public List<EditorBlock> Restore() =>
        JsonSerializer.Deserialize(_json, BlockJsonContext.Default.ListEditorBlock) ?? [];
}
