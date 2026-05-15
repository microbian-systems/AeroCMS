using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Cards;

public sealed class Card6EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.cards.6";

    public string DisplayName => "Card 6";

    public string? Description => "Dark profile card with avatar, social links, and projects list.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "square";

    public int SortOrder => 99;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(Card6BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(Card6BlockEditor);

    public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            MainText = "Claire Mac",
            FeatureItems = Card6Block.DefaultProjects.Select(ToEditorFeature).ToList()
        };
    }

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToCardBlock(editorBlock);
        return Card6BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToCardBlock(editorBlock);

    private static Card6Block ToCardBlock(EditorBlock editorBlock)
    {
        return new Card6Block
        {
            Name = FirstNonEmpty(editorBlock.MainText, editorBlock.Title, editorBlock.PageTitle, "Claire Mac"),
            AvatarUrl = FirstNonEmpty(editorBlock.Src, "https://images.unsplash.com/photo-1614644147724-2d4785d69962?auto=format&fit=crop&q=80&w=1160"),
            Projects = editorBlock.FeatureItems.Count > 0
                ? editorBlock.FeatureItems.Select(ToCardProject).ToList()
                : Card6Block.DefaultProjects.Select(CloneProject).ToList()
        };
    }

    private static AeroFeatureItem ToEditorFeature(Card6Project p) => new()
    {
        Title = p.Title,
        Description = p.Description
    };

    private static Card6Project ToCardProject(AeroFeatureItem item) => new()
    {
        Title = item.Title ?? string.Empty,
        Description = item.Description ?? string.Empty,
        Url = item.LinkUrl ?? "#"
    };

    private static Card6Project CloneProject(Card6Project p) => new()
    {
        Title = p.Title,
        Description = p.Description,
        Url = p.Url
    };

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
