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
        /// <summary>
    /// Gets or sets the Preview Mode.
    /// </summary>
bool PreviewMode { get; }

        /// <summary>
    /// SelectBlock method.
    /// </summary>
void SelectBlock(string editorId);
        /// <summary>
    /// BlockChanged method.
    /// </summary>
void BlockChanged(EditorBlock block);
        /// <summary>
    /// CompositionChanged method.
    /// </summary>
void CompositionChanged(EditorBlock block, CompositionMutation mutation);
        /// <summary>
    /// CompositionDropRejected method.
    /// </summary>
void CompositionDropRejected(string message);
        /// <summary>
    /// OpenNodeEditor method.
    /// </summary>
void OpenNodeEditor(EditorBlock block, string nodeId);
        /// <summary>
    /// OpenBlockEditor method.
    /// </summary>
void OpenBlockEditor(EditorBlock block);
        /// <summary>
    /// OpenMediaSelector method.
    /// </summary>
void OpenMediaSelector(EditorBlock block, bool multiSelect = false, string field = "src");
        /// <summary>
    /// OpenNodeMediaSelector method.
    /// </summary>
void OpenNodeMediaSelector(
        EditorBlock block,
        string nodeId,
        string field,
        EditorBreakpoint breakpoint);
        /// <summary>
    /// OpenNodeMediaSelector method.
    /// </summary>
void OpenNodeMediaSelector(
        string nodeId,
        string field,
        EditorBreakpoint breakpoint);
        /// <summary>
    /// OpenAudioSelector method.
    /// </summary>
void OpenAudioSelector(EditorBlock block);
        /// <summary>
    /// RemoveImage method.
    /// </summary>
void RemoveImage(EditorBlock block);
        /// <summary>
    /// RemoveVideo method.
    /// </summary>
void RemoveVideo(EditorBlock block);
        /// <summary>
    /// LoadVideo method.
    /// </summary>
void LoadVideo(EditorBlock block);
        /// <summary>
    /// RefreshDynamicTemplatePreviewAsync method.
    /// </summary>
Task RefreshDynamicTemplatePreviewAsync(EditorBlock block);
        /// <summary>
    /// LoadNestedVideo method.
    /// </summary>
void LoadNestedVideo(NestedBlock nb);
        /// <summary>
    /// OpenMediaSelectorForNested method.
    /// </summary>
void OpenMediaSelectorForNested(EditorBlock parent, int colIndex, NestedBlock nb);
        /// <summary>
    /// GetReferenceItems method.
    /// </summary>
List<ReferenceItem> GetReferenceItems(string type);
        /// <summary>
    /// Gets or sets the Dynamic Template Preview Html.
    /// </summary>
Dictionary<string, string> DynamicTemplatePreviewHtml { get; }
        /// <summary>
    /// RenderDynamicTemplateIfCached method.
    /// </summary>
string RenderDynamicTemplateIfCached(EditorBlock block);
}
