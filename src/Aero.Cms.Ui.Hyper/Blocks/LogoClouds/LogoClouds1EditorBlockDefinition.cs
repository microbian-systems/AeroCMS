using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.LogoClouds;

public sealed class LogoClouds1EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.logo-clouds.1";
    public string DisplayName => "Logo Clouds 1";
    public string? Description => "Simple grid of grayscale logo SVGs.";
    public string Category => "Hyper";
    public string Kind => "Block";
    public string IconName => "layers";
    public int SortOrder => 73;
    public bool PublicStaticSsrSafe => true;
    public Type? PreviewComponentType => typeof(LogoClouds1BlockEditorPreview);
    public Type? PropertyEditorComponentType => typeof(LogoClouds1BlockEditor);

    public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId
        };
    }

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToLogoCloudsBlock(editorBlock);
        return LogoClouds1BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToLogoCloudsBlock(editorBlock);

    private static LogoClouds1Block ToLogoCloudsBlock(EditorBlock editorBlock)
    {
        return new LogoClouds1Block
        {
            LogoItems = LogoCloudsDefaults.CloneDefaults()
        };
    }
}
