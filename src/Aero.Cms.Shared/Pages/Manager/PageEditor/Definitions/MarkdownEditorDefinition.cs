using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.Definitions;

public sealed class MarkdownEditorDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "markdown";
    public string DisplayName => "Markdown Block";
    public string? Description => null;
    public string Category => "Legacy UI";
    public string Kind => "block";
    public string IconName => "code";
    public int SortOrder => 0;
    public bool PublicStaticSsrSafe => true;
    public Type? PreviewComponentType => typeof(AeroUi.Legacy.MarkdownBlockPreview);
    public Type? PropertyEditorComponentType => null;

    public EditorBlock CreateDefaultEditorBlock() => new()
    {
        Type = CatalogId,
        Content = "## Heading\n\nEnter markdown here...",
        MarkdownView = "edit"
    };

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock) => null!;

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => null;
}
