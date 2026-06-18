using System.Text.Json;
using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.Definitions;

public sealed class LegacyDynamicTemplateBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "dynamic_template";
    public string DisplayName => "Dynamic Template";
    public string? Description => null;
    public string Category => "Legacy UI";
    public string Kind => "Block";
    public string IconName => "code";
    public int SortOrder => 0;
    public bool PublicStaticSsrSafe => true;
    public Type? PreviewComponentType => null;
    public Type? PropertyEditorComponentType => null;

    public EditorBlock CreateDefaultEditorBlock() => new()
    {
        Type = CatalogId,
        ScribanTemplate = "",
        ScribanDataJson = "{}"
    };

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToBlock(editorBlock);
        return ScribanBlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToBlock(editorBlock);

    private static ScribanBlock ToBlock(EditorBlock editor) => new()
    {
        Name = "Scriban Block",
        Template = editor.ScribanTemplate,
        Data = !string.IsNullOrWhiteSpace(editor.ScribanDataJson) && editor.ScribanDataJson != "{}"
            ? JsonDocument.Parse(editor.ScribanDataJson)
            : null
    };
}
