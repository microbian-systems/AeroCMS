using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Footers;

/// <summary>
/// Represents a class for Footer2EditorBlockDefinition.
/// </summary>
public sealed class Footer2EditorBlockDefinition : IPageEditorBlockDefinition
{
        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public string CatalogId => "hyper.footers.2";

        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public string DisplayName => "Footer 2";

        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description => "Logo, social links, and link columns footer.";

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
public int SortOrder => 41;

        /// <summary>
    /// Gets or sets the Public Static Ssr Safe.
    /// </summary>
public bool PublicStaticSsrSafe => true;

        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public Type? PreviewComponentType => typeof(Footer2BlockEditorPreview);

        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public Type? PropertyEditorComponentType => typeof(Footer2BlockEditor);

        /// <summary>
    /// CreateDefaultEditorBlock method.
    /// </summary>
public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            Title = "Footer 2"
        };
    }

        /// <summary>
    /// ToNeoPageNode method.
    /// </summary>
public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToFooterBlock(editorBlock);
        return Footer2BlockMapper.ToNode(block);
    }

        /// <summary>
    /// ToBlockBase method.
    /// </summary>
public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToFooterBlock(editorBlock);

    private static Footer2Block ToFooterBlock(EditorBlock editorBlock) => new();

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
