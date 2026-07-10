using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Buttons;

/// <summary>
/// Represents a class for Button3EditorBlockDefinition.
/// </summary>
public sealed class Button3EditorBlockDefinition : IPageEditorBlockDefinition
{
        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public string CatalogId => "hyper.buttons.3";

        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public string DisplayName => "Button 3";

        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description => "Gradient border button with rectangular and pill variants.";

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
public int SortOrder => 137;

        /// <summary>
    /// Gets or sets the Public Static Ssr Safe.
    /// </summary>
public bool PublicStaticSsrSafe => true;

        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public Type? PreviewComponentType => typeof(Button3BlockEditorPreview);

        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public Type? PropertyEditorComponentType => typeof(Button3BlockEditor);

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
            Button1Style = "sm"
        };
    }

        /// <summary>
    /// ToNeoPageNode method.
    /// </summary>
public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToButtonBlock(editorBlock);
        return Button3BlockMapper.ToNode(block);
    }

        /// <summary>
    /// ToBlockBase method.
    /// </summary>
public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToButtonBlock(editorBlock);

    private static Button3Block ToButtonBlock(EditorBlock editorBlock)
    {
        return new Button3Block
        {
            Text = FirstNonEmpty(editorBlock.CtaText, editorBlock.Title, "Download"),
            Url = FirstNonEmpty(editorBlock.CtaUrl, "#"),
            RoundedStyle = FirstNonEmpty(editorBlock.Button1Style, "sm")
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
}
