using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Shared.Pages.Manager.PageEditor.AeroUi;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.Definitions;

public sealed class AeroFeaturesDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "aero_features";
    public string DisplayName => "Aero Features";
    public string? Description => null;
    public string Category => "Aero UX";
    public string Kind => "block";
    public string IconName => "grid";
    public int SortOrder => 0;
    public bool PublicStaticSsrSafe => true;
    public Type? PreviewComponentType => typeof(AeroFeaturesPreview);
    public Type? PropertyEditorComponentType => null;

    public EditorBlock CreateDefaultEditorBlock() => new()
    {
        Type = "aero_features",
        MainText = "Features",
        SubText = "Key features description",
        FeatureItems = new List<AeroFeatureItem>()
    };

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock) => null!;
    public BlockBase? ToBlockBase(EditorBlock editorBlock) => null;
}
