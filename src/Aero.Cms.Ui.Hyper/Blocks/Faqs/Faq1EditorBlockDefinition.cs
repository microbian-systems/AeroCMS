using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Faqs;

public sealed class Faq1EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.faqs.1";

    public string DisplayName => "FAQ 1";

    public string? Description => "Bordered accordion FAQ with rounded panels and chevron toggle.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "help-circle";

    public int SortOrder => 70;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(Faq1BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(Faq1BlockEditor);

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

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToFaqBlock(editorBlock);
        return Faq1BlockMapper.ToNode(block);
    }

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
