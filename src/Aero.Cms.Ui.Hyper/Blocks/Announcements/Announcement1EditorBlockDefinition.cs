using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Announcements;

public sealed class Announcement1EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.announcements.1";

    public string DisplayName => "Announcement 1";

    public string? Description => "Simple announcement banner bar.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "info";

    public int SortOrder => 81;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(Announcement1BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(Announcement1BlockEditor);

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
        return Announcement1BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToAnnouncementBlock(editorBlock);

    private static Announcement1Block ToAnnouncementBlock(EditorBlock editorBlock)
    {
        return new Announcement1Block
        {
            MainText = FirstNonEmpty(editorBlock.MainText, "Lorem, ipsum dolor"),
            CtaText = FirstNonEmpty(editorBlock.CtaText, "sit amet consectetur"),
            CtaUrl = FirstNonEmpty(editorBlock.CtaUrl, "#")
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
