using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Footers;

/// <summary>
/// Represents a class for Footer11EditorBlockDefinition.
/// </summary>
public sealed class Footer11EditorBlockDefinition : IPageEditorBlockDefinition
{
        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public string CatalogId => "hyper.footers.11";
        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public string DisplayName => "Footer 11";
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description => "Simple footer with logo and copyright.";
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
public string IconName => "panel-bottom";
        /// <summary>
    /// Gets or sets the Sort Order.
    /// </summary>
public int SortOrder => 50;
        /// <summary>
    /// Gets or sets the Public Static Ssr Safe.
    /// </summary>
public bool PublicStaticSsrSafe => true;
        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public Type? PreviewComponentType => typeof(Footer11BlockEditorPreview);
        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public Type? PropertyEditorComponentType => typeof(Footer11BlockEditor);

        /// <summary>
    /// CreateDefaultEditorBlock method.
    /// </summary>
public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            Description = "Copyright &copy; 2022. All rights reserved."
        };
    }

        /// <summary>
    /// ToNeoPageNode method.
    /// </summary>
public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToFooterBlock(editorBlock);
        return Footer11BlockMapper.ToNode(block);
    }

        /// <summary>
    /// ToBlockBase method.
    /// </summary>
public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToFooterBlock(editorBlock);

    private static Footer11Block ToFooterBlock(EditorBlock editorBlock)
    {
        return new Footer11Block
        {
            Copyright = FirstNonEmpty(editorBlock.Description, editorBlock.SubText, editorBlock.MainText, "Copyright &copy; 2022. All rights reserved.")
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
