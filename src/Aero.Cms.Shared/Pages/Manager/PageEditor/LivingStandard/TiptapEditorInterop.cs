using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.LivingStandard;

/// <summary>
/// Owns the JavaScript module and editor handle used to control one Tiptap editor instance.
/// </summary>
/// <param name="js">The JavaScript runtime used to import and invoke the editor module.</param>
/// <remarks>
/// Call <see cref="InitializeAsync"/> before executing commands or reading the document.
/// Disposal releases both the JavaScript editor handle and the imported module. A browser
/// disconnect during disposal is treated as an already-released client resource.
/// </remarks>
public sealed class TiptapEditorInterop(IJSRuntime js) : IAsyncDisposable
{
    /// <summary>
    /// The static-web-asset path of the Tiptap integration module.
    /// </summary>
    internal const string ModulePath = "./_content/Aero.Cms.Shared/js/aero-tiptap-editor.js";

    /// <summary>
    /// The JavaScript export that creates an editor and returns its handle.
    /// </summary>
    internal const string InitializeMethod = "initialize";

    /// <summary>
    /// The JavaScript export that dispatches an allowlisted formatting command.
    /// </summary>
    internal const string ExecuteMethod = "execute";

    /// <summary>
    /// The JavaScript export that serializes the current ProseMirror document.
    /// </summary>
    internal const string GetDocumentJsonMethod = "getDocumentJson";

    /// <summary>
    /// The JavaScript export that releases an editor handle.
    /// </summary>
    internal const string DisposeMethod = "dispose";

    private IJSObjectReference? _module;
    private string? _handle;

    /// <summary>
    /// Imports the Tiptap module, creates an editor in the supplied element, and retains the
    /// returned handle for subsequent calls.
    /// </summary>
    /// <param name="element">The rendered element that will host the editor.</param>
    /// <param name="content">The initial HTML accepted by the browser-side editor.</param>
    /// <param name="callbackReference">
    /// The .NET callback target used to report formatting-state changes.
    /// </param>
    /// <returns>A value task that completes after the browser editor has been created.</returns>
    /// <exception cref="JSException">The module import or editor initialization fails.</exception>
    public async ValueTask InitializeAsync(
        ElementReference element,
        string content,
        DotNetObjectReference<HtmlRichTextEditorDialog> callbackReference)
    {
        var module = await GetModuleAsync();
        _handle = await module.InvokeAsync<string>(
            InitializeMethod,
            element,
            content,
            callbackReference);
    }

    /// <summary>
    /// Dispatches a formatting command to the initialized browser editor.
    /// </summary>
    /// <param name="command">The command name understood by the Tiptap integration module.</param>
    /// <param name="argument">An optional command argument, such as a link target.</param>
    /// <returns>
    /// <see langword="true"/> when the browser-side command was applied; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// <see cref="InitializeAsync"/> has not completed successfully.
    /// </exception>
    /// <exception cref="JSException">The JavaScript invocation fails.</exception>
    public async ValueTask<bool> ExecuteAsync(string command, string? argument = null)
    {
        var module = await GetInitializedModuleAsync();
        return await module.InvokeAsync<bool>(ExecuteMethod, _handle, command, argument);
    }

    /// <summary>
    /// Reads the initialized editor's ProseMirror document as JSON.
    /// </summary>
    /// <returns>The serialized document returned by the browser module.</returns>
    /// <exception cref="InvalidOperationException">
    /// <see cref="InitializeAsync"/> has not completed successfully.
    /// </exception>
    /// <exception cref="JSException">The JavaScript invocation fails.</exception>
    public async ValueTask<string> GetDocumentJsonAsync()
    {
        var module = await GetInitializedModuleAsync();
        return await module.InvokeAsync<string>(GetDocumentJsonMethod, _handle);
    }

    /// <summary>
    /// Imports the editor module once and reuses it for the lifetime of this instance.
    /// </summary>
    /// <returns>The cached or newly imported JavaScript module.</returns>
    private async ValueTask<IJSObjectReference> GetModuleAsync() =>
        _module ??= await js.InvokeAsync<IJSObjectReference>("import", ModulePath);

    /// <summary>
    /// Obtains the module only after verifying that an editor handle exists.
    /// </summary>
    /// <returns>The imported JavaScript module.</returns>
    /// <exception cref="InvalidOperationException">The editor has not been initialized.</exception>
    private async ValueTask<IJSObjectReference> GetInitializedModuleAsync()
    {
        if (_handle is null)
        {
            throw new InvalidOperationException("The Tiptap editor has not been initialized.");
        }

        return await GetModuleAsync();
    }

    /// <summary>
    /// Releases the browser editor and imported module when the JavaScript connection remains
    /// available.
    /// </summary>
    /// <returns>A value task that completes after owned JavaScript resources are released.</returns>
    /// <remarks>
    /// <see cref="JSDisconnectedException"/> is suppressed because the browser already discarded
    /// the resources when its circuit disconnected. Other JavaScript failures are observable.
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_module is not null)
            {
                if (_handle is not null)
                {
                    await _module.InvokeVoidAsync(DisposeMethod, _handle);
                }

                await _module.DisposeAsync();
            }
        }
        catch (JSDisconnectedException)
        {
        }
    }
}
