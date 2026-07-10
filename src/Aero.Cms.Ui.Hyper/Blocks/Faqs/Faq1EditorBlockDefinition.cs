using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Faqs;

/// <summary>
/// Represents a class for Faq1EditorBlockDefinition.
/// </summary>
public sealed class Faq1EditorBlockDefinition : IPageEditorBlockDefinition
{
        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public string CatalogId => "hyper.faqs.1";

        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public string DisplayName => "FAQ 1";

        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description => "Bordered accordion FAQ with rounded panels and chevron toggle.";

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
public string IconName => "help-circle";

        /// <summary>
    /// Gets or sets the Sort Order.
    /// </summary>
public int SortOrder => 70;

        /// <summary>
    /// Gets or sets the Public Static Ssr Safe.
    /// </summary>
public bool PublicStaticSsrSafe => true;

        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public Type? PreviewComponentType => typeof(Faq1BlockEditorPreview);

        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public Type? PropertyEditorComponentType => typeof(Faq1BlockEditor);

        /// <summary>
    /// CreateDefaultEditorBlock method.
    /// </summary>
public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            MainText = "FAQs",
            Description = "",
            FaqItems = Faq1Block.DefaultItems.Select(ToEditorItem).ToList()
        };
    }

        /// <summary>
    /// ToNeoPageNode method.
    /// </summary>
public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToFaqBlock(editorBlock);
        return Faq1BlockMapper.ToNode(block);
    }

        /// <summary>
    /// ToBlockBase method.
    /// </summary>
public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToFaqBlock(editorBlock);

    private static Faq1Block ToFaqBlock(EditorBlock editorBlock)
    {
        return new Faq1Block
        {
            Title = FirstNonEmpty(editorBlock.MainText, editorBlock.Title, editorBlock.PageTitle, "FAQs"),
            Description = FirstNonEmpty(editorBlock.Description, editorBlock.SubText, editorBlock.PageDescription, ""),
            Items = editorBlock.FaqItems.Count > 0
                ? editorBlock.FaqItems.Select(ToFaqItem).ToList()
                : Faq1Block.DefaultItems.Select(CloneItem).ToList()
        };
    }

    private static AeroFaqItem ToEditorItem(AeroFaqItem item) => new()
    {
        Question = item.Question,
        Answer = item.Answer
    };

    private static AeroFaqItem ToFaqItem(AeroFaqItem item) => new()
    {
        Question = item.Question ?? string.Empty,
        Answer = item.Answer ?? string.Empty
    };

    private static AeroFaqItem CloneItem(AeroFaqItem item) => new()
    {
        Question = item.Question,
        Answer = item.Answer
    };

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
