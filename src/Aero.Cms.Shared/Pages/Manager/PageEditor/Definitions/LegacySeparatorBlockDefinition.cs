using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.Definitions;

public sealed class LegacySeparatorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "separator";
    public string DisplayName => "Separator";
    public string? Description => null;
    public string Category => "Legacy UI";
    public string Kind => "Block";
    public string IconName => "minus";
    public int SortOrder => 0;
    public bool PublicStaticSsrSafe => true;
    public Type? PreviewComponentType => null;
    public Type? PropertyEditorComponentType => null;

    public EditorBlock CreateDefaultEditorBlock() => new()
    {
        Type = CatalogId
    };

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToBlock(editorBlock);
        return SeparatorBlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToBlock(editorBlock);

    private static SeparatorBlock ToBlock(EditorBlock editor) => new();
}
