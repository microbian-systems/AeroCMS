using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.Definitions;

public sealed class BoringHeroEditorDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "boring_hero";
    public string DisplayName => "Boring Hero";
    public string? Description => null;
    public string Category => "Aero UI";
    public string Kind => "block";
    public string IconName => "layout";
    public int SortOrder => 0;
    public bool PublicStaticSsrSafe => true;
    public Type? PreviewComponentType => typeof(AeroUi.Legacy.BoringHeroPreview);
    public Type? PropertyEditorComponentType => null;

    public EditorBlock CreateDefaultEditorBlock() => new()
    {
        Type = CatalogId,
        MainText = "Hero Title",
        SubText = "Hero subtitle",
        FullWidth = true
    };

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock) => null!;

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => null;
}
