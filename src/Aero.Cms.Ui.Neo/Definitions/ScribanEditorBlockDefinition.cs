using System.Text.Json;
using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Shared.Pages.Manager.PageEditor.AeroUi.Dynamic;

namespace Aero.Cms.Ui.Neo.Definitions;

public sealed class ScribanEditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "neo.template.scriban";
    public string DisplayName => "Scriban Template";
    public string? Description => "Render dynamic content using Scriban templates.";
    public string Category => "Dynamic";
    public string Kind => "Block";
    public string IconName => "code";
    public int SortOrder => 100;
    public bool PublicStaticSsrSafe => true;
    public Type? PreviewComponentType => null;
    public Type? PropertyEditorComponentType => typeof(ScribanBlockEditor);

    public EditorBlock CreateDefaultEditorBlock() => new()
    {
        Type = CatalogId,
        ScribanTemplate = "{{ title }}",
        ScribanDataJson = "{ \"title\": \"Hello\" }"
    };

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToBlock(editorBlock);
        return ScribanBlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToBlock(editorBlock);

    private static ScribanBlock ToBlock(EditorBlock editor) => new()
    {
        Name = "Scriban Block",
        Template = editor.ScribanTemplate,
        Data = !string.IsNullOrWhiteSpace(editor.ScribanDataJson) && editor.ScribanDataJson != "{}"
            ? JsonDocument.Parse(editor.ScribanDataJson)
            : null
    };
}
