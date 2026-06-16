using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Shared.Pages.Manager.PageEditor.AeroUi.Hero01;
using Aero.Cms.Shared.Pages.Manager.PageEditor.AeroUi.UI;

namespace Aero.Cms.Ui.Neo.Definitions;

public sealed class SeparatorEditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "ui.separator";
    public string DisplayName => "Separator";
    public string? Description => "A horizontal divider line.";
    public string Category => "UI";
    public string Kind => "Block";
    public string IconName => "minus";
    public int SortOrder => 80;
    public bool PublicStaticSsrSafe => true;
    public Type? PreviewComponentType => typeof(SeparatorBlockEditorPreview);
    public Type? PropertyEditorComponentType => typeof(SeparatorBlockEditor);

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
