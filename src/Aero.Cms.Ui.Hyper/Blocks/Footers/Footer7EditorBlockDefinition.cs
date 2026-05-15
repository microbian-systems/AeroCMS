using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Footers;

public sealed class Footer7EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.footers.7";

    public string DisplayName => "Footer 7";

    public string? Description => "Newsletter signup with description, social links, and link columns.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "panel-bottom";

    public int SortOrder => 46;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(Footer7BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(Footer7BlockEditor);

    public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            Title = "Want us to email you with the latest blockbuster news?",
            Description = "Newsletter footer with social links and link columns."
        };
    }

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToFooterBlock(editorBlock);
        return Footer7BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToFooterBlock(editorBlock);

    private static Footer7Block ToFooterBlock(EditorBlock editorBlock)
    {
        return new Footer7Block
        {
            NewsletterTitle = FirstNonEmpty(editorBlock.Title, editorBlock.MainText, "Want us to email you with the latest blockbuster news?"),
            NewsletterDescription = FirstNonEmpty(editorBlock.Description, editorBlock.SubText, ""),
            EmailPlaceholder = FirstNonEmpty(editorBlock.Description, "john@doe.com"),
            ButtonText = FirstNonEmpty(editorBlock.CtaText, "Subscribe"),
            CopyrightText = FirstNonEmpty(editorBlock.Description, "&copy; Company 2022. All rights reserved."),
            CreatedWithText = FirstNonEmpty(editorBlock.Description, "Created with Laravel and Laravel Livewire.")
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
