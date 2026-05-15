using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.BlogCards;

public sealed class BlogCard1EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.blog-cards.1";

    public string DisplayName => "Blog Card 1";

    public string? Description => "Classic blog card with image, date, title, and description.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "file-text";

    public int SortOrder => 87;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(BlogCard1BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(BlogCard1BlockEditor);

    public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            MainText = "How to position your furniture for positivity",
            Description = "Lorem ipsum dolor sit amet, consectetur adipisicing elit.",
            Src = "https://images.unsplash.com/photo-1524758631624-e2822e304c36?auto=format&fit=crop&q=80&w=1160",
            CtaUrl = "#"
        };
    }

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToBlogCardBlock(editorBlock);
        return BlogCard1BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToBlogCardBlock(editorBlock);

    private static BlogCard1Block ToBlogCardBlock(EditorBlock editorBlock)
    {
        return new BlogCard1Block
        {
            ImageUrl = FirstNonEmpty(editorBlock.Src, "https://images.unsplash.com/photo-1524758631624-e2822e304c36?auto=format&fit=crop&q=80&w=1160"),
            MainText = FirstNonEmpty(editorBlock.MainText, "How to position your furniture for positivity"),
            Description = FirstNonEmpty(editorBlock.Description, "Lorem ipsum dolor sit amet, consectetur adipisicing elit."),
            PublishedAt = FirstNonEmpty(editorBlock.SubText, "10th Oct 2022"),
            CtaUrl = FirstNonEmpty(editorBlock.CtaUrl, "#")
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
