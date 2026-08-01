using Aero.Cms.Abstractions.Ai.Assistant;
using Aero.Cms.Abstractions.Ai.Memory;
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
    private bool _showHistory;
    private bool _showMemories;
    private bool _isLoadingHistory;
    private bool _isEditingMemory;
    private string? _historyError;
    private string _memoryLabel = string.Empty;
    private string _memoryContent = string.Empty;
    private long? _editingMemoryId;
    private long? _editingSourceConversationId;
    private long? _pendingDeleteConversationId;
    private long? _pendingDeleteMemoryId;
    private IReadOnlyList<AeroCmsAssistantConversationSummary> _conversations = [];
    private IReadOnlyList<AeroAiExplicitMemory> _memories = [];
    internal string Draft { get => _draft; set => _draft = value; }
    private bool CanSaveMemory =>
        !string.IsNullOrWhiteSpace(_memoryLabel) &&
        _memoryLabel.Length <= AeroAiMemoryLimits.MaximumMemoryLabelCharacters &&
        !string.IsNullOrWhiteSpace(_memoryContent) &&
        _memoryContent.Length <= AeroAiMemoryLimits.MaximumMemoryContentCharacters &&
        !_isLoadingHistory;

    private async Task ToggleHistoryAsync()
    {
        if (_showHistory)
        {
            _showHistory = false;
            _pendingDeleteConversationId = null;
            _pendingDeleteMemoryId = null;
            CancelMemoryEdit();
            return;
        }

        _showHistory = true;
        _showMemories = false;
        await LoadConversationsAsync();
    }

    private async Task ShowConversationsAsync()
    {
        _showMemories = false;
        CancelMemoryEdit();
        await LoadConversationsAsync();
    }

    private async Task ShowMemoriesAsync()
    {
        _showMemories = true;
        _pendingDeleteConversationId = null;
        CancelMemoryEdit();
        await LoadMemoriesAsync();
    }

    private async Task LoadConversationsAsync()
    {
        _isLoadingHistory = true;
        _historyError = null;
        var result = await Client.ListConversationsAsync();
        if (result is Result<IReadOnlyList<AeroCmsAssistantConversationSummary>>.Ok ok)
            _conversations = ok.Value;
        else
            _historyError = "Conversation history is unavailable.";
        _isLoadingHistory = false;
    }

    private async Task LoadMemoriesAsync()
    {
        _isLoadingHistory = true;
        _historyError = null;
        var result = await Client.ListMemoriesAsync();
        if (result is Result<IReadOnlyList<AeroAiExplicitMemory>>.Ok ok)
            _memories = ok.Value;
        else
            _historyError = "Confirmed memories are unavailable.";
        _isLoadingHistory = false;
    }

    private async Task LoadConversationAsync(long conversationId)
    {
        if (State.IsSending)
            return;
        _historyError = null;
        var result = await Client.GetConversationAsync(conversationId);
        if (result is Result<AeroCmsAssistantConversation>.Ok ok)
        {
            State.Restore(ok.Value);
            _showHistory = false;
            _pendingDeleteConversationId = null;
            return;
        }
        _historyError = "That conversation could not be loaded.";
    }

    private async Task DeleteConversationAsync(long conversationId)
    {
        if (_pendingDeleteConversationId != conversationId)
        {
            _pendingDeleteConversationId = conversationId;
            return;
        }

        _historyError = null;
        var result = await Client.DeleteConversationAsync(conversationId);
        if (result is Result<bool>.Ok)
        {
            _conversations = _conversations
                .Where(conversation => conversation.ConversationId != conversationId)
                .ToArray();
            _pendingDeleteConversationId = null;
            if (State.ConversationId == conversationId)
                State.Reset();
            return;
        }
        _historyError = "That conversation could not be deleted.";
    }

    private void StartNewMemory()
    {
        _editingMemoryId = null;
        _editingSourceConversationId = State.ConversationId;
        _memoryLabel = string.Empty;
        _memoryContent = string.Empty;
        _pendingDeleteMemoryId = null;
        _isEditingMemory = true;
    }

    private void StartNewForCurrentView()
    {
        if (_showMemories)
            StartNewMemory();
        else
            StartNewConversation();
    }

    private void EditMemory(AeroAiExplicitMemory memory)
    {
        _editingMemoryId = memory.Id;
        _editingSourceConversationId = memory.SourceConversationId;
        _memoryLabel = memory.Label;
        _memoryContent = memory.Content;
        _pendingDeleteMemoryId = null;
        _isEditingMemory = true;
    }

    private void CancelMemoryEdit()
    {
        _isEditingMemory = false;
        _editingMemoryId = null;
        _editingSourceConversationId = null;
        _memoryLabel = string.Empty;
        _memoryContent = string.Empty;
    }

    private async Task SaveMemoryAsync()
    {
        if (!CanSaveMemory)
            return;

        _isLoadingHistory = true;
        _historyError = null;
        var result = await Client.SaveMemoryAsync(new AeroAiExplicitMemoryWrite(
            _memoryLabel.Trim(),
            _memoryContent.Trim(),
            _editingSourceConversationId,
            MemoryId: _editingMemoryId));
        _isLoadingHistory = false;
        if (result is Result<AeroAiExplicitMemory>.Ok)
        {
            CancelMemoryEdit();
            await LoadMemoriesAsync();
            return;
        }
        _historyError = "That memory could not be saved.";
    }

    private async Task DeleteMemoryAsync(long memoryId)
    {
        if (_pendingDeleteMemoryId != memoryId)
        {
            _pendingDeleteMemoryId = memoryId;
            return;
        }

        _historyError = null;
        var result = await Client.DeleteMemoryAsync(memoryId);
        if (result is Result<bool>.Ok)
        {
            _memories = _memories.Where(memory => memory.Id != memoryId).ToArray();
            _pendingDeleteMemoryId = null;
            if (_editingMemoryId == memoryId)
                CancelMemoryEdit();
            return;
        }
        _historyError = "That memory could not be deleted.";
    }

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
                    case AeroCmsAssistantEventKind.Metadata:
                        State.AcceptMetadata(item.ConversationId, item.Citations);
                        break;
                    case AeroCmsAssistantEventKind.Delta:
                        State.AppendDelta(item.Data);
                        break;
                    case AeroCmsAssistantEventKind.Complete:
                        State.Complete(item.Data, item.Citations);
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
    private void StartNewConversation()
    {
        State.Reset();
        _showHistory = false;
        _showMemories = false;
        _pendingDeleteConversationId = null;
        _pendingDeleteMemoryId = null;
        CancelMemoryEdit();
    }

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
