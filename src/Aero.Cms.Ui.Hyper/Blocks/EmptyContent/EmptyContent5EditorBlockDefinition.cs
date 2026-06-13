using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.EmptyContent;

public sealed class EmptyContent5EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.empty-content.5";

    public string DisplayName => "Empty Content 5";

    public string? Description => "Out of stock message with notify and explore buttons.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "inbox";

    public int SortOrder => 122;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(EmptyContent5BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(EmptyContent5BlockEditor);

    public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            MainText = "Out of stock",
            Description = "This item is currently unavailable. Check back soon or explore similar products.",
            CtaText = "Notify When Available",
            CtaUrl = "#",
            CtaText2 = "Explore Similar Products",
            CtaUrl2 = "#"
        };
    }

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToEmptyContentBlock(editorBlock);
        return EmptyContent5BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToEmptyContentBlock(editorBlock);

    private static EmptyContent5Block ToEmptyContentBlock(EditorBlock editorBlock)
    {
        return new EmptyContent5Block
        {
            Title = FirstNonEmpty(editorBlock.Title, editorBlock.MainText, editorBlock.PageTitle, "Out of stock"),
            Description = FirstNonEmpty(editorBlock.Description, editorBlock.SubText, editorBlock.PageDescription, "This item is currently unavailable."),
            CtaText = FirstNonEmpty(editorBlock.CtaText, "Notify When Available"),
            CtaUrl = string.IsNullOrWhiteSpace(editorBlock.CtaUrl) ? "#" : editorBlock.CtaUrl,
            CtaText2 = FirstNonEmpty(editorBlock.CtaText2, "Explore Similar Products"),
            CtaUrl2 = string.IsNullOrWhiteSpace(editorBlock.CtaUrl2) ? "#" : editorBlock.CtaUrl2,
            StatusText = "Last restocked: 3 weeks ago"
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
