using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.Definitions;

public sealed class TextEditorDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "text";
    public string DisplayName => "Text Block";
    public string? Description => null;
    public string Category => "Legacy UI";
    public string Kind => "block";
    public string IconName => "type";
    public int SortOrder => 0;
    public bool PublicStaticSsrSafe => true;
    public Type? PreviewComponentType => typeof(AeroUi.Legacy.TextBlockPreview);
    public Type? PropertyEditorComponentType => null;

    public EditorBlock CreateDefaultEditorBlock() => new()
    {
        Type = CatalogId,
        Content = "<p>Enter your text here...</p>"
    };

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock) => null!;

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => null;
}
