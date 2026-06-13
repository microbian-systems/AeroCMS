using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.BlogCards;

public sealed class BlogCard4EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.blog-cards.4";

    public string DisplayName => "Blog Card 4";

    public string? Description => "Minimal blog card with date, title, and category tags.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "file-text";

    public int SortOrder => 90;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(BlogCard4BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(BlogCard4BlockEditor);

    public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            MainText = "How to center an element using JavaScript and jQuery",
            SubText = "10th Oct 2022",
            CtaUrl = "#"
        };
    }

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToBlogCardBlock(editorBlock);
        return BlogCard4BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToBlogCardBlock(editorBlock);

    private static BlogCard4Block ToBlogCardBlock(EditorBlock editorBlock)
    {
        return new BlogCard4Block
        {
            MainText = FirstNonEmpty(editorBlock.MainText, "How to center an element using JavaScript and jQuery"),
            PublishedAt = FirstNonEmpty(editorBlock.SubText, "10th Oct 2022"),
            Tags = editorBlock.FeatureItems.Count > 0
                ? editorBlock.FeatureItems.Select(f => f.Title ?? "").Where(t => !string.IsNullOrEmpty(t)).ToList()
                : ["Snippet", "JavaScript"],
            CtaUrl = FirstNonEmpty(editorBlock.CtaUrl, "#")
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
