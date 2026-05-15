using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.LogoClouds;

public sealed class LogoClouds4EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.logo-clouds.4";
    public string DisplayName => "Logo Clouds 4";
    public string? Description => "Rounded grid with background cells, no text.";
    public string Category => "Hyper";
    public string Kind => "Block";
    public string IconName => "layers";
    public int SortOrder => 76;
    public bool PublicStaticSsrSafe => true;
    public Type? PreviewComponentType => typeof(LogoClouds4BlockEditorPreview);
    public Type? PropertyEditorComponentType => typeof(LogoClouds4BlockEditor);

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
        return LogoClouds4BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToLogoCloudsBlock(editorBlock);

    private static LogoClouds4Block ToLogoCloudsBlock(EditorBlock editorBlock)
    {
        return new LogoClouds4Block
        {
            LogoItems = LogoCloudsDefaults.CloneDefaults()
        };
    }
}
