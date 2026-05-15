using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Announcements;

public sealed class Announcement2EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.announcements.2";

    public string DisplayName => "Announcement 2";

    public string? Description => "Announcement banner bar with dismiss button.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "info";

    public int SortOrder => 82;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(Announcement2BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(Announcement2BlockEditor);

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
        return Announcement2BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToAnnouncementBlock(editorBlock);

    private static Announcement2Block ToAnnouncementBlock(EditorBlock editorBlock)
    {
        return new Announcement2Block
        {
            MainText = FirstNonEmpty(editorBlock.MainText, "Lorem, ipsum dolor"),
            CtaText = FirstNonEmpty(editorBlock.CtaText, "sit amet consectetur"),
            CtaUrl = FirstNonEmpty(editorBlock.CtaUrl, "#")
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
