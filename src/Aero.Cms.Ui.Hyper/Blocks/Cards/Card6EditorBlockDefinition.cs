using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Cards;

/// <summary>
/// Represents a class for Card6EditorBlockDefinition.
/// </summary>
public sealed class Card6EditorBlockDefinition : IPageEditorBlockDefinition
{
        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public string CatalogId => "hyper.cards.6";

        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public string DisplayName => "Card 6";

        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description => "Dark profile card with avatar, social links, and projects list.";

        /// <summary>
    /// Gets or sets the Category.
    /// </summary>
public string Category => "Hyper";

        /// <summary>
    /// Gets or sets the Kind.
    /// </summary>
public string Kind => "Block";

        /// <summary>
    /// Gets or sets the Icon Name.
    /// </summary>
public string IconName => "square";

        /// <summary>
    /// Gets or sets the Sort Order.
    /// </summary>
public int SortOrder => 99;

        /// <summary>
    /// Gets or sets the Public Static Ssr Safe.
    /// </summary>
public bool PublicStaticSsrSafe => true;

        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public Type? PreviewComponentType => typeof(Card6BlockEditorPreview);

        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public Type? PropertyEditorComponentType => typeof(Card6BlockEditor);

        /// <summary>
    /// CreateDefaultEditorBlock method.
    /// </summary>
public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            MainText = "Claire Mac",
            FeatureItems = Card6Block.DefaultProjects.Select(ToEditorFeature).ToList()
        };
    }

        /// <summary>
    /// ToNeoPageNode method.
    /// </summary>
public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToCardBlock(editorBlock);
        return Card6BlockMapper.ToNode(block);
    }

        /// <summary>
    /// ToBlockBase method.
    /// </summary>
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
