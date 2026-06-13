using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.LogoClouds;

public sealed class LogoClouds3EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.logo-clouds.3";
    public string DisplayName => "Logo Clouds 3";
    public string? Description => "Left-aligned title + rounded grid with background cells.";
    public string Category => "Hyper";
    public string Kind => "Block";
    public string IconName => "layers";
    public int SortOrder => 75;
    public bool PublicStaticSsrSafe => true;
    public Type? PreviewComponentType => typeof(LogoClouds3BlockEditorPreview);
    public Type? PropertyEditorComponentType => typeof(LogoClouds3BlockEditor);

    public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            MainText = "Trusted by many",
            Description = "Lorem, ipsum dolor sit amet consectetur adipisicing elit."
        };
    }

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToLogoCloudsBlock(editorBlock);
        return LogoClouds3BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToLogoCloudsBlock(editorBlock);

    private static LogoClouds3Block ToLogoCloudsBlock(EditorBlock editorBlock)
    {
        return new LogoClouds3Block
        {
            Title = FirstNonEmpty(editorBlock.MainText, editorBlock.Title, "Trusted by many"),
            Description = FirstNonEmpty(editorBlock.Description, editorBlock.SubText, "Lorem, ipsum dolor sit amet consectetur adipisicing elit."),
            LogoItems = LogoCloudsDefaults.CloneDefaults()
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
