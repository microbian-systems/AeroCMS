using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Cards;

/// <summary>
/// Represents a class for Card5EditorBlockDefinition.
/// </summary>
public sealed class Card5EditorBlockDefinition : IPageEditorBlockDefinition
{
        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public string CatalogId => "hyper.cards.5";

        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public string DisplayName => "Card 5";

        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description => "Real estate card with price, address, and feature badges.";

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
public int SortOrder => 98;

        /// <summary>
    /// Gets or sets the Public Static Ssr Safe.
    /// </summary>
public bool PublicStaticSsrSafe => true;

        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public Type? PreviewComponentType => typeof(Card5BlockEditorPreview);

        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public Type? PropertyEditorComponentType => typeof(Card5BlockEditor);

        /// <summary>
    /// CreateDefaultEditorBlock method.
    /// </summary>
public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            MainText = "123 Wallaby Avenue, Park Road",
            Description = "$240,000",
            FeatureItems = Card5Block.DefaultFeatures.Select(ToEditorFeature).ToList()
        };
    }

        /// <summary>
    /// ToNeoPageNode method.
    /// </summary>
public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToCardBlock(editorBlock);
        return Card5BlockMapper.ToNode(block);
    }

        /// <summary>
    /// ToBlockBase method.
    /// </summary>
public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToCardBlock(editorBlock);

    private static Card5Block ToCardBlock(EditorBlock editorBlock)
    {
        return new Card5Block
        {
            Title = FirstNonEmpty(editorBlock.MainText, editorBlock.Title, editorBlock.PageTitle, "123 Wallaby Avenue, Park Road"),
            Price = FirstNonEmpty(editorBlock.Description, editorBlock.SubText, editorBlock.PageDescription, "$240,000"),
            ImageUrl = FirstNonEmpty(editorBlock.Src, "https://images.unsplash.com/photo-1613545325278-f24b0cae1224?auto=format&fit=crop&q=80&w=1160"),
            Features = editorBlock.FeatureItems.Count > 0
                ? editorBlock.FeatureItems.Select(ToCardFeature).ToList()
                : Card5Block.DefaultFeatures.Select(CloneFeature).ToList(),
            CtaUrl = FirstNonEmpty(editorBlock.CtaUrl, "#")
        };
    }

    private static AeroFeatureItem ToEditorFeature(Card5Feature f) => new()
    {
        Title = f.Label,
        Description = f.Value
    };

    private static Card5Feature ToCardFeature(AeroFeatureItem item) => new()
    {
        Label = item.Title ?? string.Empty,
        Value = item.Description ?? string.Empty,
        SvgPath = string.Empty
    };

    private static Card5Feature CloneFeature(Card5Feature f) => new()
    {
        Label = f.Label,
        Value = f.Value,
        SvgPath = f.SvgPath
    };

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
