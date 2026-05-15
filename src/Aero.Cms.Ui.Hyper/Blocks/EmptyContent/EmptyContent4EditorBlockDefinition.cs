using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.EmptyContent;

public sealed class EmptyContent4EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.empty-content.4";

    public string DisplayName => "Empty Content 4";

    public string? Description => "Explore more message with link cards and back to shopping CTA.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "inbox";

    public int SortOrder => 121;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(EmptyContent4BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(EmptyContent4BlockEditor);

    public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            MainText = "Explore more",
            Description = "This section doesn't have content right now. Discover related topics and inspiration instead.",
            CtaText = "Back to Shopping",
            CtaUrl = "#",
            FeatureItems = EmptyContent4Block.DefaultLinks.Select(ToEditorLink).ToList()
        };
    }

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToEmptyContentBlock(editorBlock);
        return EmptyContent4BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToEmptyContentBlock(editorBlock);

    private static EmptyContent4Block ToEmptyContentBlock(EditorBlock editorBlock)
    {
        return new EmptyContent4Block
        {
            Title = FirstNonEmpty(editorBlock.Title, editorBlock.MainText, editorBlock.PageTitle, "Explore more"),
            Description = FirstNonEmpty(editorBlock.Description, editorBlock.SubText, editorBlock.PageDescription, "This section doesn't have content right now."),
            CtaText = FirstNonEmpty(editorBlock.CtaText, "Back to Shopping"),
            CtaUrl = string.IsNullOrWhiteSpace(editorBlock.CtaUrl) ? "#" : editorBlock.CtaUrl,
            Links = editorBlock.FeatureItems.Count > 0
                ? editorBlock.FeatureItems.Select(ToEmptyContentLink).ToList()
                : EmptyContent4Block.DefaultLinks.Select(CloneLink).ToList()
        };
    }

    private static AeroFeatureItem ToEditorLink(EmptyContentLink l) => new()
    {
        Title = l.Title,
        Description = l.Description,
        LinkUrl = l.Url
    };

    private static EmptyContentLink ToEmptyContentLink(AeroFeatureItem f) => new()
    {
        Title = f.Title ?? string.Empty,
        Description = f.Description ?? string.Empty,
        Url = string.IsNullOrWhiteSpace(f.LinkUrl) ? "#" : f.LinkUrl!
    };

    private static EmptyContentLink CloneLink(EmptyContentLink l) => new()
    {
        Title = l.Title,
        Description = l.Description,
        Url = l.Url
    };

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
