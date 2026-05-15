using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Faqs;

public sealed class Faq3EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.faqs.3";

    public string DisplayName => "FAQ 3";

    public string? Description => "Left-border-accented accordion FAQ with chevron toggle.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "help-circle";

    public int SortOrder => 72;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(Faq3BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(Faq3BlockEditor);

    public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            MainText = "FAQs",
            Description = "",
            FaqItems = Faq3Block.DefaultItems.Select(ToEditorItem).ToList()
        };
    }

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToFaqBlock(editorBlock);
        return Faq3BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToFaqBlock(editorBlock);

    private static Faq3Block ToFaqBlock(EditorBlock editorBlock)
    {
        return new Faq3Block
        {
            Title = FirstNonEmpty(editorBlock.MainText, editorBlock.Title, editorBlock.PageTitle, "FAQs"),
            Description = FirstNonEmpty(editorBlock.Description, editorBlock.SubText, editorBlock.PageDescription, ""),
            Items = editorBlock.FaqItems.Count > 0
                ? editorBlock.FaqItems.Select(ToFaqItem).ToList()
                : Faq3Block.DefaultItems.Select(CloneItem).ToList()
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
