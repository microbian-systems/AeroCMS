using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.Definitions;

public sealed class QuoteEditorDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "quote";
    public string DisplayName => "Quote Block";
    public string? Description => null;
    public string Category => "Legacy UI";
    public string Kind => "block";
    public string IconName => "quote";
    public int SortOrder => 0;
    public bool PublicStaticSsrSafe => true;
    public Type? PreviewComponentType => typeof(AeroUi.Legacy.QuoteBlockPreview);
    public Type? PropertyEditorComponentType => null;

    public EditorBlock CreateDefaultEditorBlock() => new()
    {
        Type = CatalogId,
        Content = "Your quote text here...",
        Author = "Author Name"
    };

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock) => null!;

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => null;
}
