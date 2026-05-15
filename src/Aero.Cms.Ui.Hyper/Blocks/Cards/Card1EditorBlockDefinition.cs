using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Cards;

public sealed class Card1EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.cards.1";

    public string DisplayName => "Card 1";

    public string? Description => "Blog/article card with avatar, title, byline, description, date and reading time.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "square";

    public int SortOrder => 94;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(Card1BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(Card1BlockEditor);

    public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            MainText = "How I built my first website with Nuxt, Tailwind CSS and Vercel",
            Description = "Lorem ipsum dolor sit, amet consectetur adipisicing elit. At velit illum provident a, ipsa maiores deleniti consectetur nobis et eaque."
        };
    }

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToCardBlock(editorBlock);
        return Card1BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToCardBlock(editorBlock);

    private static Card1Block ToCardBlock(EditorBlock editorBlock)
    {
        return new Card1Block
        {
            Title = FirstNonEmpty(editorBlock.MainText, editorBlock.Title, editorBlock.PageTitle, "How I built my first website with Nuxt, Tailwind CSS and Vercel"),
            Description = FirstNonEmpty(editorBlock.Description, editorBlock.SubText, editorBlock.PageDescription, "Lorem ipsum dolor sit, amet consectetur adipisicing elit. At velit illum provident a, ipsa maiores deleniti consectetur nobis et eaque."),
            AvatarUrl = FirstNonEmpty(editorBlock.Src, "https://images.unsplash.com/photo-1633332755192-727a05c4013d?auto=format&fit=crop&q=80&w=1160"),
            CtaUrl = FirstNonEmpty(editorBlock.CtaUrl, "#")
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
