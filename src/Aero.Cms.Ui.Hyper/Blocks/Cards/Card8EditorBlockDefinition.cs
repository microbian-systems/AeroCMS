using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Cards;

public sealed class Card8EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.cards.8";

    public string DisplayName => "Card 8";

    public string? Description => "Podcast episode card with badge, title, description, duration, and featuring.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "square";

    public int SortOrder => 101;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(Card8BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(Card8BlockEditor);

    public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            MainText = "Some Interesting Podcast Title",
            Description = "Lorem ipsum, dolor sit amet consectetur adipisicing elit. Ipsam nulla amet voluptatum sit rerum, atque, quo culpa ut necessitatibus eius suscipit eum accusamus, aperiam voluptas exercitationem facere aliquid fuga. Sint."
        };
    }

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToCardBlock(editorBlock);
        return Card8BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToCardBlock(editorBlock);

    private static Card8Block ToCardBlock(EditorBlock editorBlock)
    {
        return new Card8Block
        {
            Title = FirstNonEmpty(editorBlock.MainText, editorBlock.Title, editorBlock.PageTitle, "Some Interesting Podcast Title"),
            Description = FirstNonEmpty(editorBlock.Description, editorBlock.SubText, editorBlock.PageDescription, "Lorem ipsum, dolor sit amet consectetur adipisicing elit. Ipsam nulla amet voluptatum sit rerum, atque, quo culpa ut necessitatibus eius suscipit eum accusamus, aperiam voluptas exercitationem facere aliquid fuga. Sint."),
            CtaUrl = FirstNonEmpty(editorBlock.CtaUrl, "#")
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
