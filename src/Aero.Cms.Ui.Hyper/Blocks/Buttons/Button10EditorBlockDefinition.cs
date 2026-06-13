using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Buttons;

public sealed class Button10EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.buttons.10";

    public string DisplayName => "Button 10";

    public string? Description => "Reveal underline/side bar on hover from left, right, bottom, or top.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "square";

    public int SortOrder => 144;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(Button10BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(Button10BlockEditor);

    public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            CtaText = "Download",
            CtaUrl = "#",
            Button1Style = "left"
        };
    }

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToButtonBlock(editorBlock);
        return Button10BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToButtonBlock(editorBlock);

    private static Button10Block ToButtonBlock(EditorBlock editorBlock)
    {
        return new Button10Block
        {
            Text = FirstNonEmpty(editorBlock.CtaText, editorBlock.Title, "Download"),
            Url = FirstNonEmpty(editorBlock.CtaUrl, "#"),
            RevealDirection = FirstNonEmpty(editorBlock.Button1Style, "left")
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
}
