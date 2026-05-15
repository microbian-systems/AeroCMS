using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Announcements;

public sealed class Announcement6EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.announcements.6";

    public string DisplayName => "Announcement 6";

    public string? Description => "Floating bottom announcement card with dismiss button.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "info";

    public int SortOrder => 86;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(Announcement6BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(Announcement6BlockEditor);

    public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            MainText = "Lorem, ipsum dolor",
            CtaText = "sit amet consectetur",
            CtaUrl = "#"
        };
    }

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToAnnouncementBlock(editorBlock);
        return Announcement6BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToAnnouncementBlock(editorBlock);

    private static Announcement6Block ToAnnouncementBlock(EditorBlock editorBlock)
    {
        return new Announcement6Block
        {
            MainText = FirstNonEmpty(editorBlock.MainText, "Lorem, ipsum dolor"),
            CtaText = FirstNonEmpty(editorBlock.CtaText, "sit amet consectetur"),
            CtaUrl = FirstNonEmpty(editorBlock.CtaUrl, "#")
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
