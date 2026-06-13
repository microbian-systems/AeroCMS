using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Cards;

public sealed class Card7EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.cards.7";

    public string DisplayName => "Card 7";

    public string? Description => "Portfolio card with image, company name, and category.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "square";

    public int SortOrder => 100;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(Card7BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(Card7BlockEditor);

    public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            MainText = "Company Name",
            Description = "Branding / Signage"
        };
    }

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToCardBlock(editorBlock);
        return Card7BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToCardBlock(editorBlock);

    private static Card7Block ToCardBlock(EditorBlock editorBlock)
    {
        return new Card7Block
        {
            Title = FirstNonEmpty(editorBlock.MainText, editorBlock.Title, editorBlock.PageTitle, "Company Name"),
            Subtitle = FirstNonEmpty(editorBlock.Description, editorBlock.SubText, editorBlock.PageDescription, "Branding / Signage"),
            ImageUrl = FirstNonEmpty(editorBlock.Src, "https://images.unsplash.com/photo-1588515724527-074a7a56616c?auto=format&fit=crop&q=80&w=1160"),
            CtaUrl = FirstNonEmpty(editorBlock.CtaUrl, "#")
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
