using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.BlogCards;

public sealed class BlogCard2EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.blog-cards.2";

    public string DisplayName => "Blog Card 2";

    public string? Description => "Blog card with shadow image, title, and description with group hover.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "file-text";

    public int SortOrder => 88;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(BlogCard2BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(BlogCard2BlockEditor);

    public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            MainText = "Finding the Journey to Mordor",
            Description = "Lorem ipsum dolor sit amet, consectetur adipisicing elit.",
            Src = "https://images.unsplash.com/photo-1631451095765-2c91616fc9e6?auto=format&fit=crop&q=80&w=1160",
            CtaUrl = "#"
        };
    }

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToBlogCardBlock(editorBlock);
        return BlogCard2BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToBlogCardBlock(editorBlock);

    private static BlogCard2Block ToBlogCardBlock(EditorBlock editorBlock)
    {
        return new BlogCard2Block
        {
            ImageUrl = FirstNonEmpty(editorBlock.Src, "https://images.unsplash.com/photo-1631451095765-2c91616fc9e6?auto=format&fit=crop&q=80&w=1160"),
            MainText = FirstNonEmpty(editorBlock.MainText, "Finding the Journey to Mordor"),
            Description = FirstNonEmpty(editorBlock.Description, "Lorem ipsum dolor sit amet, consectetur adipisicing elit."),
            CtaUrl = FirstNonEmpty(editorBlock.CtaUrl, "#")
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
