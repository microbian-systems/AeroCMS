using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Serialization;

namespace Aero.Cms.Abstractions.Blocks.Editor;

public sealed class EditorBlockListMemento
{
    private readonly string _json;

    private EditorBlockListMemento(string json)
    {
        _json = json;
    }

    public static EditorBlockListMemento Capture(IReadOnlyList<EditorBlock> blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);

        return new EditorBlockListMemento(
            JsonSerializer.Serialize(blocks, BlockJsonContext.Default.IReadOnlyListEditorBlock));
    }

    public List<EditorBlock> Restore() =>
        JsonSerializer.Deserialize(_json, BlockJsonContext.Default.ListEditorBlock) ?? [];
}
