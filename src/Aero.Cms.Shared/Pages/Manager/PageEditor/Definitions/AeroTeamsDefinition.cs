using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Shared.Pages.Manager.PageEditor.AeroUi;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.Definitions;

public sealed class AeroTeamsDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "aero_teams";
    public string DisplayName => "Aero Teams";
    public string? Description => null;
    public string Category => "Aero UX";
    public string Kind => "block";
    public string IconName => "users";
    public int SortOrder => 0;
    public bool PublicStaticSsrSafe => true;
    public Type? PreviewComponentType => typeof(AeroTeamsPreview);
    public Type? PropertyEditorComponentType => null;

    public EditorBlock CreateDefaultEditorBlock() => new()
    {
        Type = "aero_teams",
        MainText = "Our Team",
        Description = "Meet the team"
    };

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock) => null!;
    public BlockBase? ToBlockBase(EditorBlock editorBlock) => null;
}
