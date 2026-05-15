using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.EmptyContent;

public sealed class EmptyContent3EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.empty-content.3";

    public string DisplayName => "Empty Content 3";

    public string? Description => "Coming soon message with email notification signup.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "inbox";

    public int SortOrder => 120;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(EmptyContent3BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(EmptyContent3BlockEditor);

    public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            MainText = "Coming soon!",
            Description = "We're working on something exciting. Be the first to know when it launches."
        };
    }

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToEmptyContentBlock(editorBlock);
        return EmptyContent3BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToEmptyContentBlock(editorBlock);

    private static EmptyContent3Block ToEmptyContentBlock(EditorBlock editorBlock)
    {
        return new EmptyContent3Block
        {
            Title = FirstNonEmpty(editorBlock.Title, editorBlock.MainText, editorBlock.PageTitle, "Coming soon!"),
            Description = FirstNonEmpty(editorBlock.Description, editorBlock.SubText, editorBlock.PageDescription, "We're working on something exciting."),
            EmailPlaceholder = "your@email.com",
            SubmitText = "Notify Me",
            Footnote = "We'll let you know the moment it's available."
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
