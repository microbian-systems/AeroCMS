using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Ctas;

/// <summary>
/// Represents a class for Cta3EditorBlockDefinition.
/// </summary>
public sealed class Cta3EditorBlockDefinition : IPageEditorBlockDefinition
{
        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public string CatalogId => "hyper.ctas.3";

        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public string DisplayName => "CTA 3";

        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description => "Side-by-side CTA with curved image and emerald button.";

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
public string IconName => "megaphone";

        /// <summary>
    /// Gets or sets the Sort Order.
    /// </summary>
public int SortOrder => 68;

        /// <summary>
    /// Gets or sets the Public Static Ssr Safe.
    /// </summary>
public bool PublicStaticSsrSafe => true;

        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public Type? PreviewComponentType => typeof(Cta3BlockEditorPreview);

        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public Type? PropertyEditorComponentType => typeof(Cta3BlockEditor);

        /// <summary>
    /// CreateDefaultEditorBlock method.
    /// </summary>
public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            MainText = "Lorem, ipsum dolor sit amet consectetur adipisicing elit",
            Description = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Et, egestas tempus tellus etiam sed.",
            CtaText = "Get Started Today",
            CtaUrl = "#",
            Src = "https://images.unsplash.com/photo-1484959014842-cd1d967a39cf?auto=format&fit=crop&q=80&w=1160"
        };
    }

        /// <summary>
    /// ToNeoPageNode method.
    /// </summary>
public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToCtaBlock(editorBlock);
        return Cta3BlockMapper.ToNode(block);
    }

        /// <summary>
    /// ToBlockBase method.
    /// </summary>
public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToCtaBlock(editorBlock);

    private static Cta3Block ToCtaBlock(EditorBlock editorBlock)
    {
        return new Cta3Block
        {
            Title = FirstNonEmpty(editorBlock.MainText, editorBlock.Title, "Lorem, ipsum dolor sit amet consectetur adipisicing elit"),
            Description = FirstNonEmpty(editorBlock.Description, editorBlock.SubText, "Lorem ipsum dolor sit amet, consectetur adipiscing elit."),
            CtaText = FirstNonEmpty(editorBlock.CtaText, "Get Started Today"),
            CtaUrl = FirstNonEmpty(editorBlock.CtaUrl, "#"),
            ImageUrl = FirstNonEmpty(editorBlock.Src, editorBlock.BackgroundImage, "https://images.unsplash.com/photo-1484959014842-cd1d967a39cf?auto=format&fit=crop&q=80&w=1160")
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
