using Aero.Cms.Abstractions.Ai.Assistant;
using Aero.Cms.Shared.Services;
using Aero.Cms.Contracts.Services;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Components;
using System.Text.Json;

namespace Aero.Cms.Shared.Layout;

public partial class ManagerAssistantDrawer : ComponentBase, IDisposable
{
    [Inject] internal ManagerAssistantState State { get; set; } = default!;
    [Inject] internal IMcpAssistantHttpClient Client { get; set; } = default!;
    [Inject] private AppState AppState { get; set; } = default!;

    private CancellationTokenSource? _sendCts;
    private ElementReference _messageEnd;
    private string _draft = string.Empty;
    internal string Draft { get => _draft; set => _draft = value; }

    protected override void OnInitialized()
    {
        State.Changed += OnStateChanged;
        AppState.StateChanged += OnAppStateChanged;
        State.SynchronizeContext(AppState.UserId, AppState.CurrentSiteId);
    }

    internal async Task SendAsync()
    {
        var prompt = _draft.Trim();
        if (State.IsSending || string.IsNullOrWhiteSpace(prompt))
            return;

        _draft = string.Empty;
        AeroCmsAssistantRequest request;
        try
        {
            request = State.Begin(prompt);
        }
        catch (ArgumentException)
        {
            State.Fail("The message is invalid or too long.");
            return;
        }

        var localCts = new CancellationTokenSource();
        var previousCts = Interlocked.Exchange(ref _sendCts, localCts);
        previousCts?.Cancel();
        try
        {
            var streamResult = await Client.StreamAsync(request, localCts.Token);
            if (!IsCurrent(localCts))
                return;
            if (streamResult is Result<IAsyncEnumerable<AeroCmsAssistantEvent>>.Failure)
            {
                State.Fail("The assistant is unavailable. Try again shortly.");
                return;
            }

            var stream = ((Result<IAsyncEnumerable<AeroCmsAssistantEvent>>.Ok)streamResult).Value;
            await foreach (var item in stream.WithCancellation(localCts.Token))
            {
                if (!IsCurrent(localCts))
                    return;
                switch (item.Kind)
                {
                    case AeroCmsAssistantEventKind.Delta:
                        State.AppendDelta(item.Data);
                        break;
                    case AeroCmsAssistantEventKind.Complete:
                        State.Complete(item.Data);
                        break;
                    case AeroCmsAssistantEventKind.Error:
                        State.Fail(item.Data ?? "The assistant could not complete the response.");
                        break;
                }
            }

            if (IsCurrent(localCts) && State.IsSending)
                State.Complete(finalText: null);
        }
        catch (OperationCanceledException)
        {
            if (IsCurrent(localCts))
                State.Cancel();
        }
        catch (Exception exception) when (
            exception is HttpRequestException or IOException or JsonException or InvalidDataException)
        {
            if (IsCurrent(localCts))
                State.Fail("The assistant connection was interrupted. Try again.");
        }
        finally
        {
            Interlocked.CompareExchange(ref _sendCts, null, localCts);
            localCts.Dispose();
        }
    }

    private bool IsCurrent(CancellationTokenSource localCts)
        => ReferenceEquals(Volatile.Read(ref _sendCts), localCts);

    private void Cancel() => Volatile.Read(ref _sendCts)?.Cancel();
    private void Close() => State.Close();
    private void Reset() => State.Reset();

    private void OnStateChanged() => _ = InvokeAsync(StateHasChanged);

    private void OnAppStateChanged()
        => SynchronizeContext(AppState.UserId, AppState.CurrentSiteId);

    internal void SynchronizeContext(long? userId, long? siteId, bool notifyRender = true)
    {
        Volatile.Read(ref _sendCts)?.Cancel();
        State.SynchronizeContext(userId, siteId);
        if (notifyRender)
            _ = InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        State.Changed -= OnStateChanged;
        AppState.StateChanged -= OnAppStateChanged;
        Interlocked.Exchange(ref _sendCts, null)?.Cancel();
    }
}
