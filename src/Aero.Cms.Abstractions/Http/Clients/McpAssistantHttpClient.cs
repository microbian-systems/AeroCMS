using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Aero.Cms.Abstractions.Ai.Assistant;
using Aero.Cms.Abstractions.Ai.Memory;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Abstractions.Http.Clients;

/// <summary>Consumes the manager assistant REST and POST-SSE endpoints.</summary>
public sealed class McpAssistantHttpClient(HttpClient httpClient) : IMcpAssistantHttpClient
{
    private const string CompletePath = "api/v1/admin/mcp/assistant/complete";
    private const string StreamPath = "api/v1/admin/mcp/assistant/stream";
    private const string ConversationsPath = "api/v1/admin/mcp/assistant/conversations";
    private const string MemoriesPath = "api/v1/admin/mcp/assistant/memories";

    public async Task<Result<AeroCmsAssistantResponse>> CompleteAsync(
        AeroCmsAssistantRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await httpClient.PostAsJsonAsync(CompletePath, request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return AeroError.HttpRequestError(response.StatusCode, "Assistant request failed.");

            var result = await response.Content.ReadFromJsonAsync<AeroCmsAssistantResponse>(cancellationToken);
            return result is null
                ? AeroError.CreateError("Assistant returned an empty response.")
                : result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return AeroError.CancelledError("Assistant request was cancelled.");
        }
        catch (Exception)
        {
            return AeroError.HttpRequestError(HttpStatusCode.ServiceUnavailable, "Assistant request failed.");
        }
    }

    public async Task<Result<IAsyncEnumerable<AeroCmsAssistantEvent>>> StreamAsync(
        AeroCmsAssistantRequest request,
        CancellationToken cancellationToken = default)
    {
        HttpResponseMessage? response = null;
        try
        {
            var message = new HttpRequestMessage(HttpMethod.Post, StreamPath)
            {
                Content = JsonContent.Create(request)
            };
            message.Headers.Accept.ParseAdd("text/event-stream");
            response = await httpClient.SendAsync(
                message,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (ShouldUseRestFallback(response.StatusCode))
            {
                response.Dispose();
                var fallback = await CompleteAsync(request, cancellationToken);
                return fallback switch
                {
                    Result<AeroCmsAssistantResponse>.Ok ok =>
                        new Result<IAsyncEnumerable<AeroCmsAssistantEvent>>.Ok(RestFallback(ok.Value, cancellationToken)),
                    Result<AeroCmsAssistantResponse>.Failure failure => failure.Error,
                    _ => AeroError.CreateError("Assistant fallback failed.")
                };
            }

            if (!response.IsSuccessStatusCode)
            {
                var status = response.StatusCode;
                response.Dispose();
                return AeroError.HttpRequestError(status, "Assistant stream failed.");
            }

            return new Result<IAsyncEnumerable<AeroCmsAssistantEvent>>.Ok(
                ReadResponseAsync(response, cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            response?.Dispose();
            return AeroError.CancelledError("Assistant stream was cancelled.");
        }
        catch (Exception)
        {
            response?.Dispose();
            return AeroError.HttpRequestError(HttpStatusCode.ServiceUnavailable, "Assistant stream failed.");
        }
    }

    public async Task<Result<IReadOnlyList<AeroCmsAssistantConversationSummary>>> ListConversationsAsync(
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await httpClient.GetAsync(
                $"{ConversationsPath}?take={take}",
                cancellationToken);
            if (!response.IsSuccessStatusCode)
                return AeroError.HttpRequestError(response.StatusCode, "Conversation history request failed.");
            var result = await response.Content
                .ReadFromJsonAsync<AeroCmsAssistantConversationSummary[]>(cancellationToken);
            return result ?? [];
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return AeroError.CancelledError("Conversation history request was cancelled.");
        }
        catch (Exception)
        {
            return AeroError.HttpRequestError(
                HttpStatusCode.ServiceUnavailable,
                "Conversation history request failed.");
        }
    }

    public async Task<Result<AeroCmsAssistantConversation>> GetConversationAsync(
        long conversationId,
        CancellationToken cancellationToken = default)
    {
        if (conversationId <= 0)
            return AeroError.ValidationError(["Conversation identifiers must be positive."]);
        try
        {
            using var response = await httpClient.GetAsync(
                $"{ConversationsPath}/{conversationId}",
                cancellationToken);
            if (!response.IsSuccessStatusCode)
                return AeroError.HttpRequestError(response.StatusCode, "Conversation history request failed.");
            var result = await response.Content
                .ReadFromJsonAsync<AeroCmsAssistantConversation>(cancellationToken);
            return result is null
                ? AeroError.CreateError("Conversation history returned an empty response.")
                : result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return AeroError.CancelledError("Conversation history request was cancelled.");
        }
        catch (Exception)
        {
            return AeroError.HttpRequestError(
                HttpStatusCode.ServiceUnavailable,
                "Conversation history request failed.");
        }
    }

    public async Task<Result<bool>> DeleteConversationAsync(
        long conversationId,
        CancellationToken cancellationToken = default)
    {
        if (conversationId <= 0)
            return AeroError.ValidationError(["Conversation identifiers must be positive."]);
        try
        {
            using var response = await httpClient.DeleteAsync(
                $"{ConversationsPath}/{conversationId}",
                cancellationToken);
            return response.IsSuccessStatusCode
                ? true
                : AeroError.HttpRequestError(response.StatusCode, "Conversation deletion failed.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return AeroError.CancelledError("Conversation deletion was cancelled.");
        }
        catch (Exception)
        {
            return AeroError.HttpRequestError(
                HttpStatusCode.ServiceUnavailable,
                "Conversation deletion failed.");
        }
    }

    public async Task<Result<IReadOnlyList<AeroAiExplicitMemory>>> ListMemoriesAsync(
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await httpClient.GetAsync(
                $"{MemoriesPath}?take={take}",
                cancellationToken);
            if (!response.IsSuccessStatusCode)
                return AeroError.HttpRequestError(response.StatusCode, "Assistant memory request failed.");
            var result = await response.Content
                .ReadFromJsonAsync<AeroAiExplicitMemory[]>(cancellationToken);
            return result ?? [];
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return AeroError.CancelledError("Assistant memory request was cancelled.");
        }
        catch (Exception)
        {
            return AeroError.HttpRequestError(
                HttpStatusCode.ServiceUnavailable,
                "Assistant memory request failed.");
        }
    }

    public async Task<Result<AeroAiExplicitMemory>> SaveMemoryAsync(
        AeroAiExplicitMemoryWrite memory,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = memory.MemoryId is long memoryId
                ? await httpClient.PutAsJsonAsync($"{MemoriesPath}/{memoryId}", memory, cancellationToken)
                : await httpClient.PostAsJsonAsync(MemoriesPath, memory, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return AeroError.HttpRequestError(response.StatusCode, "Assistant memory save failed.");
            var result = await response.Content
                .ReadFromJsonAsync<AeroAiExplicitMemory>(cancellationToken);
            return result is null
                ? AeroError.CreateError("Assistant memory save returned an empty response.")
                : result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return AeroError.CancelledError("Assistant memory save was cancelled.");
        }
        catch (Exception)
        {
            return AeroError.HttpRequestError(
                HttpStatusCode.ServiceUnavailable,
                "Assistant memory save failed.");
        }
    }

    public async Task<Result<bool>> DeleteMemoryAsync(
        long memoryId,
        CancellationToken cancellationToken = default)
    {
        if (memoryId <= 0)
            return AeroError.ValidationError(["Memory identifiers must be positive."]);
        try
        {
            using var response = await httpClient.DeleteAsync(
                $"{MemoriesPath}/{memoryId}",
                cancellationToken);
            return response.IsSuccessStatusCode
                ? true
                : AeroError.HttpRequestError(response.StatusCode, "Assistant memory deletion failed.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return AeroError.CancelledError("Assistant memory deletion was cancelled.");
        }
        catch (Exception)
        {
            return AeroError.HttpRequestError(
                HttpStatusCode.ServiceUnavailable,
                "Assistant memory deletion failed.");
        }
    }

    private static bool ShouldUseRestFallback(HttpStatusCode statusCode)
        => statusCode is HttpStatusCode.NotFound
            or HttpStatusCode.MethodNotAllowed
            or HttpStatusCode.NotAcceptable
            or HttpStatusCode.NotImplemented;

    private static async IAsyncEnumerable<AeroCmsAssistantEvent> ReadResponseAsync(
        HttpResponseMessage response,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using (response)
        await using (var stream = await response.Content.ReadAsStreamAsync(cancellationToken))
        {
            await foreach (var item in AeroCmsAssistantSseParser.ParseAsync(stream, cancellationToken))
                yield return item;
        }
    }

    private static async IAsyncEnumerable<AeroCmsAssistantEvent> RestFallback(
        AeroCmsAssistantResponse response,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        yield return new(
            AeroCmsAssistantEventKind.Metadata,
            CorrelationId: response.CorrelationId,
            ConversationId: response.ConversationId,
            Citations: response.Citations);
        yield return new(
            AeroCmsAssistantEventKind.Complete,
            response.Text,
            response.CorrelationId,
            response.ConversationId,
            response.Citations);
        await Task.CompletedTask;
    }
}

/// <summary>Incrementally parses bounded SSE frames, including fragmented and multiline data.</summary>
public static class AeroCmsAssistantSseParser
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async IAsyncEnumerable<AeroCmsAssistantEvent> ParseAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, true, 1024, leaveOpen: true);
        var data = new StringBuilder();
        string? eventName = null;

        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                if (data.Length > 0)
                {
                    var final = Deserialize(eventName, data.ToString());
                    yield return final;
                }
                yield break;
            }

            if (line.Length == 0)
            {
                if (data.Length == 0)
                {
                    eventName = null;
                    continue;
                }

                var item = Deserialize(eventName, data.ToString());
                yield return item;
                if (item.Kind is AeroCmsAssistantEventKind.Complete or AeroCmsAssistantEventKind.Error)
                    yield break;

                data.Clear();
                eventName = null;
                continue;
            }

            if (line[0] == ':')
                continue;

            var colon = line.IndexOf(':');
            var field = colon < 0 ? line : line[..colon];
            var value = colon < 0 ? string.Empty : line[(colon + 1)..].TrimStart(' ');
            if (field == "event")
            {
                eventName = value;
            }
            else if (field == "data")
            {
                if (data.Length > 0)
                    data.Append('\n');
                data.Append(value);
                if (data.Length > AeroCmsAssistantLimits.MaxEventCharacters)
                    throw new InvalidDataException("Assistant SSE event exceeded the maximum size.");
            }
        }
    }

    private static AeroCmsAssistantEvent Deserialize(string? eventName, string data)
    {
        var item = JsonSerializer.Deserialize<AeroCmsAssistantEvent>(data, JsonOptions)
            ?? throw new InvalidDataException("Assistant SSE event was empty.");
        if (TryParseKind(eventName, out var kind) && item.Kind != kind)
            item = item with { Kind = kind };
        return item;
    }

    private static bool TryParseKind(string? value, out AeroCmsAssistantEventKind kind)
        => Enum.TryParse(value, ignoreCase: true, out kind);
}
