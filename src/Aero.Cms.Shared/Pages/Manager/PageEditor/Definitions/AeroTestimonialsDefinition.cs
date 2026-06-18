using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Shared.Pages.Manager.PageEditor.AeroUi;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.Definitions;

public sealed class AeroTestimonialsDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "aero_testimonials";
    public string DisplayName => "Aero Testimonials";
    public string? Description => null;
    public string Category => "Aero UX";
    public string Kind => "block";
    public string IconName => "quote";
    public int SortOrder => 0;
    public bool PublicStaticSsrSafe => true;
    public Type? PreviewComponentType => typeof(AeroTestimonialsPreview);
    public Type? PropertyEditorComponentType => null;

    public EditorBlock CreateDefaultEditorBlock() => new()
    {
        Type = "aero_testimonials",
        MainText = "Testimonials",
        Description = "What people say"
    };

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock) => null!;
    public BlockBase? ToBlockBase(EditorBlock editorBlock) => null;
}
