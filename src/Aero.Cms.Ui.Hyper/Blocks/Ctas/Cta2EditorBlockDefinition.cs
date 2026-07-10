using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Ctas;

/// <summary>
/// Represents a class for Cta2EditorBlockDefinition.
/// </summary>
public sealed class Cta2EditorBlockDefinition : IPageEditorBlockDefinition
{
        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public string CatalogId => "hyper.ctas.2";

        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public string DisplayName => "CTA 2";

        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description => "Centered CTA with email signup form and rose button.";

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
public int SortOrder => 67;

        /// <summary>
    /// Gets or sets the Public Static Ssr Safe.
    /// </summary>
public bool PublicStaticSsrSafe => true;

        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public Type? PreviewComponentType => typeof(Cta2BlockEditorPreview);

        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public Type? PropertyEditorComponentType => typeof(Cta2BlockEditor);

        /// <summary>
    /// CreateDefaultEditorBlock method.
    /// </summary>
public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            MainText = "Lorem, ipsum dolor sit amet consectetur adipisicing elit",
            Description = "Lorem ipsum dolor sit amet, consectetur adipisicing elit.",
            CtaText = "Sign Up",
            CtaText2 = "Email address",
            CtaUrl = "#"
        };
    }

        /// <summary>
    /// ToNeoPageNode method.
    /// </summary>
public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToCtaBlock(editorBlock);
        return Cta2BlockMapper.ToNode(block);
    }

        /// <summary>
    /// ToBlockBase method.
    /// </summary>
public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToCtaBlock(editorBlock);

    private static Cta2Block ToCtaBlock(EditorBlock editorBlock)
    {
        return new Cta2Block
        {
            Title = FirstNonEmpty(editorBlock.MainText, editorBlock.Title, "Lorem, ipsum dolor sit amet consectetur adipisicing elit"),
            Description = FirstNonEmpty(editorBlock.Description, editorBlock.SubText, "Lorem ipsum dolor sit amet, consectetur adipisicing elit."),
            ButtonText = FirstNonEmpty(editorBlock.CtaText, "Sign Up"),
            Placeholder = FirstNonEmpty(editorBlock.CtaText2, "Email address"),
            FormAction = FirstNonEmpty(editorBlock.CtaUrl, "#")
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
