using Aero.Cms.Abstractions.Ai.Assistant;

namespace Aero.Cms.Shared.Services;

/// <summary>Scoped, in-memory manager assistant state that survives route navigation within one circuit.</summary>
public sealed class ManagerAssistantState
{
    private readonly List<ManagerAssistantEntry> _messages = [];
    private long? _userId;
    private long? _siteId;
    private bool _contextInitialized;

    public event Action? Changed;
    public IReadOnlyList<ManagerAssistantEntry> Messages => _messages;
    public bool IsOpen { get; private set; }
    public bool IsSending { get; private set; }

    public void Toggle()
    {
        IsOpen = !IsOpen;
        Changed?.Invoke();
    }

    public void Close()
    {
        if (!IsOpen)
            return;
        IsOpen = false;
        Changed?.Invoke();
    }

    public void SynchronizeContext(long? userId, long? siteId)
    {
        if (_contextInitialized && (_userId != userId || _siteId != siteId))
            ResetCore(close: true);
        _contextInitialized = true;
        _userId = userId;
        _siteId = siteId;
    }

    public AeroCmsAssistantRequest Begin(string userText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userText);
        if (userText.Length > AeroCmsAssistantLimits.MaxUserMessageCharacters)
            throw new ArgumentOutOfRangeException(nameof(userText));

        IsSending = true;
        _messages.Add(new(AeroCmsAssistantRole.User, userText));
        TrimHistoryForRequest();
        var request = new AeroCmsAssistantRequest(
            _messages.Select(message => new AeroCmsAssistantMessage(message.Role, message.Text)).ToList());
        _messages.Add(new(AeroCmsAssistantRole.Assistant, string.Empty, IsStreaming: true));
        Changed?.Invoke();
        return request;
    }

    public void AppendDelta(string? delta)
    {
        if (string.IsNullOrEmpty(delta) || _messages.Count == 0)
            return;
        var last = _messages[^1];
        if (last.Role != AeroCmsAssistantRole.Assistant || !last.IsStreaming)
            return;
        _messages[^1] = last with { Text = last.Text + delta };
        Changed?.Invoke();
    }

    public void Complete(string? finalText)
    {
        if (_messages.Count == 0)
            return;
        var last = _messages[^1];
        var text = string.IsNullOrWhiteSpace(finalText) ? last.Text : finalText;
        _messages[^1] = last with { Text = text, IsStreaming = false };
        IsSending = false;
        Changed?.Invoke();
    }

    public void Fail(string message)
    {
        if (_messages.Count > 0 && _messages[^1].IsStreaming)
            _messages[^1] = _messages[^1] with { Text = message, IsStreaming = false, IsError = true };
        IsSending = false;
        Changed?.Invoke();
    }

    public void Cancel()
    {
        if (_messages.Count > 0 && _messages[^1].IsStreaming)
        {
            var last = _messages[^1];
            _messages[^1] = last with
            {
                Text = string.IsNullOrWhiteSpace(last.Text) ? "Response cancelled." : last.Text,
                IsStreaming = false
            };
        }
        IsSending = false;
        Changed?.Invoke();
    }

    public void Reset()
    {
        ResetCore(close: false);
        Changed?.Invoke();
    }

    private void TrimHistoryForRequest()
    {
        while (_messages.Count > AeroCmsAssistantLimits.MaxMessages)
            _messages.RemoveAt(0);
        while (_messages.Sum(message => message.Text.Length) > AeroCmsAssistantLimits.MaxConversationCharacters &&
               _messages.Count > 1)
        {
            _messages.RemoveAt(0);
        }
    }

    private void ResetCore(bool close)
    {
        _messages.Clear();
        IsSending = false;
        if (close)
            IsOpen = false;
    }
}

/// <summary>One display entry in the manager assistant drawer.</summary>
public sealed record ManagerAssistantEntry(
    AeroCmsAssistantRole Role,
    string Text,
    bool IsStreaming = false,
    bool IsError = false);
