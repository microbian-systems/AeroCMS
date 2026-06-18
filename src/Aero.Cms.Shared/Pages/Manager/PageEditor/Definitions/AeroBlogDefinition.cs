using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Shared.Pages.Manager.PageEditor.AeroUi;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.Definitions;

public sealed class AeroBlogDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "aero_blog";
    public string DisplayName => "Aero Blog";
    public string? Description => null;
    public string Category => "Aero UX";
    public string Kind => "block";
    public string IconName => "notebook-text";
    public int SortOrder => 0;
    public bool PublicStaticSsrSafe => true;
    public Type? PreviewComponentType => typeof(AeroBlogPreview);
    public Type? PropertyEditorComponentType => null;

    public EditorBlock CreateDefaultEditorBlock() => new()
    {
        Type = "aero_blog",
        MainText = "Blog",
        Description = "Latest posts"
    };

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock) => null!;
    public BlockBase? ToBlockBase(EditorBlock editorBlock) => null;
}
