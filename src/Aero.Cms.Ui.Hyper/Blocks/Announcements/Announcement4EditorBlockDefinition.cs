using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Announcements;

public sealed class Announcement4EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.announcements.4";

    public string DisplayName => "Announcement 4";

    public string? Description => "Fixed bottom announcement banner with dismiss button.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "info";

    public int SortOrder => 84;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(Announcement4BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(Announcement4BlockEditor);

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
        return Announcement4BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToAnnouncementBlock(editorBlock);

    private static Announcement4Block ToAnnouncementBlock(EditorBlock editorBlock)
    {
        return new Announcement4Block
        {
            MainText = FirstNonEmpty(editorBlock.MainText, "Lorem, ipsum dolor"),
            CtaText = FirstNonEmpty(editorBlock.CtaText, "sit amet consectetur"),
            CtaUrl = FirstNonEmpty(editorBlock.CtaUrl, "#")
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
