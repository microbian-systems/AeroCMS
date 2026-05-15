using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Footers;

public sealed class Footer6EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.footers.6";

    public string DisplayName => "Footer 6";

    public string? Description => "Two-column layout with demo request form and link columns.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "panel-bottom";

    public int SortOrder => 45;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(Footer6BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(Footer6BlockEditor);

    public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            Title = "Request a Demo",
            Description = "Sign up form with link columns."
        };
    }

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToFooterBlock(editorBlock);
        return Footer6BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToFooterBlock(editorBlock);

    private static Footer6Block ToFooterBlock(EditorBlock editorBlock)
    {
        return new Footer6Block
        {
            CtaTitle = FirstNonEmpty(editorBlock.Title, editorBlock.MainText, "Request a Demo"),
            CtaDescription = FirstNonEmpty(editorBlock.Description, editorBlock.SubText, ""),
            EmailPlaceholder = FirstNonEmpty(editorBlock.Description, "john@rhcp.com"),
            ButtonText = FirstNonEmpty(editorBlock.CtaText, "Sign Up"),
            CopyrightText = FirstNonEmpty(editorBlock.Description, "&copy; 2022. Company Name. All rights reserved.")
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
