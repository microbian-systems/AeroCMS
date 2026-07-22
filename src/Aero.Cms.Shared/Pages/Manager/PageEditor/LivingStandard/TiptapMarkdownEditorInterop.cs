using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.LivingStandard;

/// <summary>Owns one block-capable Tiptap editor used for Markdown authoring.</summary>
public sealed class TiptapMarkdownEditorInterop(IJSRuntime js) : IAsyncDisposable
{
    internal const string ModulePath = "./_content/Aero.Cms.Shared/js/aero-tiptap-markdown-editor.js";
    private IJSObjectReference? _module;
    private string? _handle;

    /// <summary>Creates the browser editor with validated initial HTML.</summary>
    public async ValueTask InitializeAsync(ElementReference element, string content)
    {
        _module = await js.InvokeAsync<IJSObjectReference>("import", ModulePath);
        _handle = await _module.InvokeAsync<string>("initialize", element, content);
    }

    /// <summary>Executes one allowlisted Markdown-authoring command.</summary>
    public async ValueTask<bool> ExecuteAsync(string command, string? argument = null)
    {
        var module = RequireInitializedModule();
        return await module.InvokeAsync<bool>("execute", _handle, command, argument);
    }

    /// <summary>Gets the edited HTML for server-side policy validation and Markdown export.</summary>
    public async ValueTask<string> GetHtmlAsync()
    {
        var module = RequireInitializedModule();
        return await module.InvokeAsync<string>("getHtml", _handle);
    }

    private IJSObjectReference RequireInitializedModule() =>
        _module is not null && _handle is not null
            ? _module
            : throw new InvalidOperationException("The Tiptap Markdown editor has not been initialized.");

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_module is not null)
            {
                if (_handle is not null)
                {
                    await _module.InvokeVoidAsync("dispose", _handle);
                }

                await _module.DisposeAsync();
            }
        }
        catch (JSDisconnectedException)
        {
        }
    }
}
