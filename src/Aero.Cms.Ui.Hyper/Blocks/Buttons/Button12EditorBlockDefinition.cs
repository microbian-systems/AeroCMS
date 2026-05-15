using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Buttons;

public sealed class Button12EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.buttons.12";

    public string DisplayName => "Button 12";

    public string? Description => "Offset lift button with border shadow, solid red and bordered variants.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "square";

    public int SortOrder => 146;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(Button12BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(Button12BlockEditor);

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

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToButtonBlock(editorBlock);
        return Button12BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToButtonBlock(editorBlock);

    private static Button12Block ToButtonBlock(EditorBlock editorBlock)
    {
        return new Button12Block
        {
            Text = FirstNonEmpty(editorBlock.CtaText, editorBlock.Title, "Download"),
            Url = FirstNonEmpty(editorBlock.CtaUrl, "#"),
            Style = FirstNonEmpty(editorBlock.Button1Style, "solid")
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
}
