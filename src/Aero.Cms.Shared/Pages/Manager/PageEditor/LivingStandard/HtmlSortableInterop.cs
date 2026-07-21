using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.LivingStandard;

/// <summary>
/// Owns the disposable browser adapter for one living-standard canvas.
/// </summary>
public sealed class HtmlSortableInterop(IJSRuntime js) : IAsyncDisposable
{
    internal const string ModulePath = "./_content/Aero.Cms.Shared/js/aero-html-sortable.js";
    internal const string InitializeMethod = "initialize";
    internal const string DisposeMethod = "dispose";

    private IJSObjectReference? _module;
    private string? _handle;

    public async ValueTask InitializeAsync(
        ElementReference surface,
        ElementReference selectionToolbar,
        ElementReference dragHandle,
        DotNetObjectReference<HtmlPageEditorCanvas> callbackReference)
    {
        _module = await js.InvokeAsync<IJSObjectReference>("import", ModulePath);
        _handle = await _module.InvokeAsync<string>(
            InitializeMethod,
            surface,
            selectionToolbar,
            dragHandle,
            callbackReference);
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
