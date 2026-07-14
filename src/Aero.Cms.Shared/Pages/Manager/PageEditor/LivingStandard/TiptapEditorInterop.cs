using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.LivingStandard;

public sealed class TiptapEditorInterop(IJSRuntime js) : IAsyncDisposable
{
    internal const string ModulePath = "./_content/Aero.Cms.Shared/js/aero-tiptap-editor.js";
    internal const string InitializeMethod = "initialize";
    internal const string ExecuteMethod = "execute";
    internal const string GetDocumentJsonMethod = "getDocumentJson";
    internal const string DisposeMethod = "dispose";

    private IJSObjectReference? _module;
    private string? _handle;

    public async ValueTask InitializeAsync(ElementReference element, string content)
    {
        var module = await GetModuleAsync();
        _handle = await module.InvokeAsync<string>(InitializeMethod, element, content);
    }

    public async ValueTask<bool> ExecuteAsync(string command, string? argument = null)
    {
        var module = await GetInitializedModuleAsync();
        return await module.InvokeAsync<bool>(ExecuteMethod, _handle, command, argument);
    }

    public async ValueTask<string> GetDocumentJsonAsync()
    {
        var module = await GetInitializedModuleAsync();
        return await module.InvokeAsync<string>(GetDocumentJsonMethod, _handle);
    }

    private async ValueTask<IJSObjectReference> GetModuleAsync() =>
        _module ??= await js.InvokeAsync<IJSObjectReference>("import", ModulePath);

    private async ValueTask<IJSObjectReference> GetInitializedModuleAsync()
    {
        if (_handle is null)
        {
            throw new InvalidOperationException("The Tiptap editor has not been initialized.");
        }

        return await GetModuleAsync();
    }

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
