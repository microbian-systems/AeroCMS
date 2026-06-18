using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Shared.Pages.Manager.PageEditor.AeroUi;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.Definitions;

public sealed class AeroAuthDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "aero_auth";
    public string DisplayName => "Aero Auth";
    public string? Description => null;
    public string Category => "Aero UX";
    public string Kind => "block";
    public string IconName => "lock";
    public int SortOrder => 0;
    public bool PublicStaticSsrSafe => true;
    public Type? PreviewComponentType => typeof(AeroAuthPreview);
    public Type? PropertyEditorComponentType => null;

    public EditorBlock CreateDefaultEditorBlock() => new()
    {
        Type = "aero_auth",
        MainText = "Sign In",
        Description = "Authentication form",
        CtaText = "Sign In"
    };

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock) => null!;
    public BlockBase? ToBlockBase(EditorBlock editorBlock) => null;
}
