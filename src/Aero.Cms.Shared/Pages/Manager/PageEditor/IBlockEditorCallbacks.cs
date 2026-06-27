using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Layout;
using Aero.Cms.Abstractions.Blocks.Neo.Styles;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor;

/// <summary>
/// Interface for callbacks that block preview editors need from the PageEditor orchestrator.
/// Implemented by <see cref="PageEditor"/> and cascaded to <see cref="BlockEditorPreviewHost"/>.
/// </summary>
public interface IBlockEditorCallbacks
{
    bool PreviewMode { get; }

    void SelectBlock(string editorId);
    void BlockChanged(EditorBlock block);
    void CompositionChanged(EditorBlock block, CompositionMutation mutation);
    void CompositionDropRejected(string message);
    void OpenNodeEditor(EditorBlock block, string nodeId);
    void OpenBlockEditor(EditorBlock block);
    void OpenMediaSelector(EditorBlock block, bool multiSelect = false, string field = "src");
    void OpenNodeMediaSelector(
        EditorBlock block,
        string nodeId,
        string field,
        EditorBreakpoint breakpoint);
    void OpenNodeMediaSelector(
        string nodeId,
        string field,
        EditorBreakpoint breakpoint);
    void OpenAudioSelector(EditorBlock block);
    void RemoveImage(EditorBlock block);
    void RemoveVideo(EditorBlock block);
    void LoadVideo(EditorBlock block);
    Task RefreshDynamicTemplatePreviewAsync(EditorBlock block);
    void LoadNestedVideo(NestedBlock nb);
    void OpenMediaSelectorForNested(EditorBlock parent, int colIndex, NestedBlock nb);
    List<ReferenceItem> GetReferenceItems(string type);
    Dictionary<string, string> DynamicTemplatePreviewHtml { get; }
    string RenderDynamicTemplateIfCached(EditorBlock block);
}
