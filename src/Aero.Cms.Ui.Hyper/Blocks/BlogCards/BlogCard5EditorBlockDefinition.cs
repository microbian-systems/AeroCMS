using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.BlogCards;

public sealed class BlogCard5EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.blog-cards.5";

    public string DisplayName => "Blog Card 5";

    public string? Description => "Blog card with icon, title, description, and link.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "file-text";

    public int SortOrder => 91;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(BlogCard5BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(BlogCard5BlockEditor);

    public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            MainText = "Lorem ipsum dolor sit, amet consectetur adipisicing elit.",
            Description = "Lorem ipsum dolor sit amet, consectetur adipisicing elit.",
            Src = "",
            CtaText = "Find out more",
            CtaUrl = "#"
        };
    }

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToBlogCardBlock(editorBlock);
        return BlogCard5BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToBlogCardBlock(editorBlock);

    private static BlogCard5Block ToBlogCardBlock(EditorBlock editorBlock)
    {
        return new BlogCard5Block
        {
            ImageUrl = FirstNonEmpty(editorBlock.Src, ""),
            MainText = FirstNonEmpty(editorBlock.MainText, "Lorem ipsum dolor sit, amet consectetur adipisicing elit."),
            Description = FirstNonEmpty(editorBlock.Description, "Lorem ipsum dolor sit amet, consectetur adipisicing elit."),
            CtaText = FirstNonEmpty(editorBlock.CtaText, "Find out more"),
            CtaUrl = FirstNonEmpty(editorBlock.CtaUrl, "#")
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
