using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Neo.Blocks.CtaBanner;

/// <summary>
/// Represents a class for CtaBannerEditorBlockDefinition.
/// </summary>
public sealed class CtaBannerEditorBlockDefinition : IPageEditorBlockDefinition
{
        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public string CatalogId => CtaBannerBlock.BlockTypeId;

        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public string DisplayName => "CTA Banner";

        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description => "A NeoUI call-to-action banner with title, description, and dual CTAs.";

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
public string IconName => "megaphone";

        /// <summary>
    /// Gets or sets the Sort Order.
    /// </summary>
public int SortOrder => 30;

        /// <summary>
    /// Gets or sets the Public Static Ssr Safe.
    /// </summary>
public bool PublicStaticSsrSafe => false;

        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public Type? PreviewComponentType => typeof(CtaBannerBlockEditorPreview);

        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public Type? PropertyEditorComponentType => typeof(CtaBannerBlockEditor);

        /// <summary>
    /// CreateDefaultEditorBlock method.
    /// </summary>
public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type          = CatalogId,
            MainText      = "Start building for free today",
            SubText       = "Join thousands of teams already using Acme to ship faster and smarter. No credit card required.",
            CtaText       = "Get started free",
            CtaUrl        = "#",
            CtaText2      = "Schedule a demo",
            CtaUrl2       = "#",
        };
    }

        /// <summary>
    /// ToNeoPageNode method.
    /// </summary>
public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToBlock(editorBlock);
        return CtaBannerBlockMapper.ToNode(block);
    }

        /// <summary>
    /// ToBlockBase method.
    /// </summary>
public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToBlock(editorBlock);

    private static CtaBannerBlock ToBlock(EditorBlock editor) => new()
    {
        Title         = FirstNonEmpty(editor.MainText,      "Start building for free today"),
        Description   = FirstNonEmpty(editor.SubText,       string.Empty),
        PrimaryText   = FirstNonEmpty(editor.CtaText,       "Get started free"),
        PrimaryUrl    = FirstNonEmpty(editor.CtaUrl,        "#"),
        SecondaryText = FirstNonEmpty(editor.CtaText2,      "Schedule a demo"),
        SecondaryUrl  = FirstNonEmpty(editor.CtaUrl2,       "#"),
    };

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
}
