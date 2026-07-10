using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Neo.Blocks.CenteredHero;

/// <summary>
/// Represents a class for CenteredHeroEditorBlockDefinition.
/// </summary>
public sealed class CenteredHeroEditorBlockDefinition : IPageEditorBlockDefinition
{
        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public string CatalogId => CenteredHeroBlock.BlockTypeId;

        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public string DisplayName => "Centered Hero";

        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description => "A NeoUI centered hero section with eyebrow, title, highlight, dual CTAs, and trust markers.";

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
public string IconName => "sparkles";

        /// <summary>
    /// Gets or sets the Sort Order.
    /// </summary>
public int SortOrder => 10;

        /// <summary>
    /// Gets or sets the Public Static Ssr Safe.
    /// </summary>
public bool PublicStaticSsrSafe => false;

        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public Type? PreviewComponentType => typeof(CenteredHeroBlockEditorPreview);

        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public Type? PropertyEditorComponentType => typeof(CenteredHeroBlockEditor);

        /// <summary>
    /// CreateDefaultEditorBlock method.
    /// </summary>
public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type          = CatalogId,
            Eyebrow       = "Introducing NeoUI v3",
            MainText      = "Build beautiful Blazor apps",
            Highlight     = "faster than ever",
            SubText       = "100+ production-ready components for .NET Blazor. Accessible, customizable, and built for speed.",
            CtaText       = "Get started for free",
            CtaUrl        = "#",
            CtaText2      = "View on GitHub",
            CtaUrl2       = "#",
            TrustMarkers  = ["Free & open source", ".NET 8+ compatible", "Dark mode included", "100+ components"],
            BackgroundImage = string.Empty
        };
    }

        /// <summary>
    /// ToNeoPageNode method.
    /// </summary>
public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToBlock(editorBlock);
        return CenteredHeroBlockMapper.ToNode(block);
    }

        /// <summary>
    /// ToBlockBase method.
    /// </summary>
public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToBlock(editorBlock);

    private static CenteredHeroBlock ToBlock(EditorBlock editor) => new()
    {
        Eyebrow       = FirstNonEmpty(editor.Eyebrow,       "Introducing NeoUI v3"),
        Title         = FirstNonEmpty(editor.MainText,      "Build beautiful Blazor apps"),
        Highlight     = FirstNonEmpty(editor.Highlight,     "faster than ever"),
        Description   = FirstNonEmpty(editor.SubText,       string.Empty),
        PrimaryText   = FirstNonEmpty(editor.CtaText,       "Get started for free"),
        PrimaryUrl    = FirstNonEmpty(editor.CtaUrl,        "#"),
        SecondaryText = FirstNonEmpty(editor.CtaText2,      "View on GitHub"),
        SecondaryUrl  = FirstNonEmpty(editor.CtaUrl2,       "#"),
        TrustMarkers  = editor.TrustMarkers.Count > 0
            ? editor.TrustMarkers
            : [],                                              // Block model defaults handle empty-list fallback
    };

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
}
