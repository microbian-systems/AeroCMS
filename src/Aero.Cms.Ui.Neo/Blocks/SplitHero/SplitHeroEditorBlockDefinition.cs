using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Neo.Blocks.SplitHero;

/// <summary>
/// Represents a class for SplitHeroEditorBlockDefinition.
/// </summary>
public sealed class SplitHeroEditorBlockDefinition : IPageEditorBlockDefinition
{
        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public string CatalogId => SplitHeroBlock.BlockTypeId;

        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public string DisplayName => "Hero Split Layout";

        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description => "A NeoUI split hero section with eyebrow, title, description, dual CTAs, and decorative card panel.";

        /// <summary>
    /// Gets or sets the Category.
    /// </summary>
public string Category => "Neo";

        /// <summary>
    /// Gets or sets the Kind.
    /// </summary>
public string Kind => "Block";

        /// <summary>
    /// Gets or sets the Icon Name.
    /// </summary>
public string IconName => "layout-dashboard";

        /// <summary>
    /// Gets or sets the Sort Order.
    /// </summary>
public int SortOrder => 20;

        /// <summary>
    /// Gets or sets the Public Static Ssr Safe.
    /// </summary>
public bool PublicStaticSsrSafe => false;

        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public Type? PreviewComponentType => typeof(SplitHeroBlockEditorPreview);

        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public Type? PropertyEditorComponentType => typeof(SplitHeroBlockEditor);

        /// <summary>
    /// CreateDefaultEditorBlock method.
    /// </summary>
public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type          = CatalogId,
            Eyebrow       = "New — v2.0 is here",
            MainText      = "Build better products, ship faster",
            SubText       = "The all-in-one platform that helps your team design, develop, and deliver exceptional digital experiences without the complexity.",
            CtaText       = "Get started free",
            CtaUrl        = "#",
            CtaText2      = "Watch demo",
            CtaUrl2       = "#",
            BackgroundImage = string.Empty
        };
    }

        /// <summary>
    /// ToNeoPageNode method.
    /// </summary>
public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToBlock(editorBlock);
        return SplitHeroBlockMapper.ToNode(block);
    }

        /// <summary>
    /// ToBlockBase method.
    /// </summary>
public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToBlock(editorBlock);

    private static SplitHeroBlock ToBlock(EditorBlock editor) => new()
    {
        Eyebrow       = FirstNonEmpty(editor.Eyebrow,  "New — v2.0 is here"),
        Title         = FirstNonEmpty(editor.MainText, "Build better products, ship faster"),
        Description   = FirstNonEmpty(editor.SubText,  string.Empty),
        PrimaryText   = FirstNonEmpty(editor.CtaText,  "Get started free"),
        PrimaryUrl    = FirstNonEmpty(editor.CtaUrl,   "#"),
        SecondaryText = FirstNonEmpty(editor.CtaText2, "Watch demo"),
        SecondaryUrl  = FirstNonEmpty(editor.CtaUrl2,  "#"),
    };

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
}
