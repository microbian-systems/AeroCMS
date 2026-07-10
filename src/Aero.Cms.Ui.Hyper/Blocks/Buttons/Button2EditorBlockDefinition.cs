using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Buttons;

/// <summary>
/// Represents a class for Button2EditorBlockDefinition.
/// </summary>
public sealed class Button2EditorBlockDefinition : IPageEditorBlockDefinition
{
        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public string CatalogId => "hyper.buttons.2";

        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public string DisplayName => "Button 2";

        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description => "Text button with right arrow icon, solid and bordered variants.";

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
public int SortOrder => 136;

        /// <summary>
    /// Gets or sets the Public Static Ssr Safe.
    /// </summary>
public bool PublicStaticSsrSafe => true;

        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public Type? PreviewComponentType => typeof(Button2BlockEditorPreview);

        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public Type? PropertyEditorComponentType => typeof(Button2BlockEditor);

        /// <summary>
    /// CreateDefaultEditorBlock method.
    /// </summary>
public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            CtaText = "Download",
            CtaUrl = "#",
            Button1Style = "solid"
        };
    }

        /// <summary>
    /// ToNeoPageNode method.
    /// </summary>
public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToButtonBlock(editorBlock);
        return Button2BlockMapper.ToNode(block);
    }

        /// <summary>
    /// ToBlockBase method.
    /// </summary>
public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToButtonBlock(editorBlock);

    private static Button2Block ToButtonBlock(EditorBlock editorBlock)
    {
        return new Button2Block
        {
            Text = FirstNonEmpty(editorBlock.CtaText, editorBlock.Title, "Download"),
            Url = FirstNonEmpty(editorBlock.CtaUrl, "#"),
            Style = FirstNonEmpty(editorBlock.Button1Style, "solid")
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
}
