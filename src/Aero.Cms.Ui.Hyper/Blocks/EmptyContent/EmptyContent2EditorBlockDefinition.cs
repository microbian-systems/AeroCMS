using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.EmptyContent;

public sealed class EmptyContent2EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.empty-content.2";

    public string DisplayName => "Empty Content 2";

    public string? Description => "Nothing found message with flex-wrap links.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "inbox";

    public int SortOrder => 119;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(EmptyContent2BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(EmptyContent2BlockEditor);

    public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            MainText = "Hmm, nothing found",
            Description = "We couldn't find what you were looking for. Try a different search term or explore our popular categories.",
            CtaText = "Browse Popular Items",
            CtaUrl = "#",
            CtaText2 = "Refine Search",
            CtaUrl2 = "#"
        };
    }

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToEmptyContentBlock(editorBlock);
        return EmptyContent2BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToEmptyContentBlock(editorBlock);

    private static EmptyContent2Block ToEmptyContentBlock(EditorBlock editorBlock)
    {
        return new EmptyContent2Block
        {
            Title = FirstNonEmpty(editorBlock.Title, editorBlock.MainText, editorBlock.PageTitle, "Hmm, nothing found"),
            Description = FirstNonEmpty(editorBlock.Description, editorBlock.SubText, editorBlock.PageDescription, "We couldn't find what you were looking for."),
            CtaText = FirstNonEmpty(editorBlock.CtaText, "Browse Popular Items"),
            CtaUrl = string.IsNullOrWhiteSpace(editorBlock.CtaUrl) ? "#" : editorBlock.CtaUrl,
            CtaText2 = FirstNonEmpty(editorBlock.CtaText2, "Refine Search"),
            CtaUrl2 = string.IsNullOrWhiteSpace(editorBlock.CtaUrl2) ? "#" : editorBlock.CtaUrl2
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
