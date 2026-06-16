using System.Text.Json;
using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Shared.Pages.Manager.PageEditor.AeroUi.Hero01;

namespace Aero.Cms.Ui.Neo.Definitions;

public sealed class BasicHeroEditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "aero.hero.basic";
    public string DisplayName => "Basic Hero";
    public string? Description => "A hero section with headline, description, and CTA.";
    public string Category => "Components";
    public string Kind => "Block";
    public string IconName => "layout";
    public int SortOrder => 20;
    public bool PublicStaticSsrSafe => true;
    public Type? PreviewComponentType => typeof(BasicHeroBlockEditorPreview);
    public Type? PropertyEditorComponentType => typeof(BasicHeroBlockEditor);

    public EditorBlock CreateDefaultEditorBlock() => new()
    {
        Type = CatalogId,
        MainText = "Welcome",
        SubText = "Your message goes here.",
        CtaText = "",
        CtaUrl = "",
        BackgroundImage = string.Empty,
        FullWidth = true
    };

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToBlock(editorBlock);
        return BasicHeroBlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToBlock(editorBlock);

    private static BasicHeroBlock ToBlock(EditorBlock editor) => new()
    {
        Title = FirstNonEmpty(editor.MainText, "Welcome"),
        Subtitle = FirstNonEmpty(editor.SubText, "Your message goes here."),
        BackgroundImageUrl = editor.BackgroundImage,
        CtaText = editor.CtaText,
        CtaUrl = editor.CtaUrl
    };

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
}
