using Microsoft.JSInterop;

namespace Aero.Cms.Shared.Pages.Manager.Theming;

internal sealed class ThemeStudioInterop(IJSRuntime js) : IAsyncDisposable
{
    internal const string ModulePath = "./_content/Aero.Cms.Shared/Pages/Manager/Theming/ThemeStudio.razor.js";
    internal const string DownloadMethod = "download";
    internal const string ConfirmDiscardMethod = "confirmDiscard";
    private IJSObjectReference? _module;

    private async ValueTask<IJSObjectReference> GetModuleAsync(CancellationToken ct) => _module ??= await js.InvokeAsync<IJSObjectReference>("import", ct, ModulePath);
    public async ValueTask DownloadAsync(string fileName, string content, string contentType, CancellationToken ct) => await (await GetModuleAsync(ct)).InvokeVoidAsync(DownloadMethod, ct, fileName, content, contentType);
    public async ValueTask<bool> ConfirmDiscardAsync(CancellationToken ct) => await (await GetModuleAsync(ct)).InvokeAsync<bool>(ConfirmDiscardMethod, ct);

    public async ValueTask DisposeAsync()
    {
        try { if (_module is not null) await _module.DisposeAsync(); }
        catch (JSDisconnectedException) { }
    }
}
