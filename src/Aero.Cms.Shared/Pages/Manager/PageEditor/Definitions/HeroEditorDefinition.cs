using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.Definitions;

public sealed class HeroEditorDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hero";
    public string DisplayName => "Hero";
    public string? Description => null;
    public string Category => "Aero UI";
    public string Kind => "block";
    public string IconName => "layout";
    public int SortOrder => 0;
    public bool PublicStaticSsrSafe => true;
    public Type? PreviewComponentType => typeof(AeroUi.Legacy.HeroPreview);
    public Type? PropertyEditorComponentType => null;

    public EditorBlock CreateDefaultEditorBlock() => new()
    {
        Type = CatalogId,
        MainText = "Hero Title",
        SubText = "Hero subtitle",
        CtaText = "Get Started",
        CtaUrl = "#",
        Height = 400
    };

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock) => null!;

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => null;
}
