namespace Aero.Cms.Abstractions.Http.Clients;

using Aero.Cms.Abstractions.Ai;
using Aero.Cms.Abstractions.Content.Localization;
using Aero.Core.Railway;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

/// <summary>
/// Interface for AeroCMS AI manager endpoints.
/// </summary>
public interface IAiHttpClient
{
    /// <summary>
    /// Gets the AI provider configuration used by manager features.
    /// </summary>
    Task<Result<AiSettingsConfiguration, AeroError>> GetSettingsAsync(CancellationToken ct = default);

    /// <summary>
    /// Saves the AI provider configuration. API keys are write-only.
    /// </summary>
    Task<Result<AiSettingsConfiguration, AeroError>> SaveSettingsAsync(
        SaveAiSettingsRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Gets enabled and usable provider choices for content enhancement.
    /// </summary>
    Task<Result<IReadOnlyList<AiProviderOption>, AeroError>> GetProviderOptionsAsync(CancellationToken ct = default);

    /// <summary>
    /// Enhances one content field and returns a suggestion for review.
    /// </summary>
    Task<Result<EnhanceContentResponse, AeroError>> EnhanceContentAsync(
        EnhanceContentRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Streams one content-field suggestion over POST-SSE.
    /// </summary>
    Task<Result<IAsyncEnumerable<EnhanceContentEvent>, AeroError>> StreamEnhanceContentAsync(
        EnhanceContentRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Translates a document-shaped field payload and returns translated field values.
    /// </summary>
    Task<Result<TranslateDocumentResponse, AeroError>> TranslateContentAsync(
        TranslateDocumentRequest request,
        CancellationToken ct = default);

    Task<Result<GenerateContentAiTranslationResponse, AeroError>> GenerateContentTranslationAsync(
        GenerateContentAiTranslationRequest request,
        CancellationToken ct = default);
}

/// <summary>
/// Typed client for AI manager endpoints.
/// </summary>
public sealed class AiHttpClient(HttpClient httpClient, ILogger<AiHttpClient> logger)
    : AeroCmsClientBase(httpClient, logger), IAiHttpClient
{
    /// <inheritdoc />
    public override string Path => "admin/ai";

    /// <inheritdoc />
    public Task<Result<AiSettingsConfiguration, AeroError>> GetSettingsAsync(CancellationToken ct = default)
    {
        return GetAsync<AiSettingsConfiguration>("settings", ct);
    }

    /// <inheritdoc />
    public Task<Result<AiSettingsConfiguration, AeroError>> SaveSettingsAsync(
        SaveAiSettingsRequest request,
        CancellationToken ct = default)
    {
        return PostAsync<SaveAiSettingsRequest, AiSettingsConfiguration>("settings", request, ct);
    }

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<AiProviderOption>, AeroError>> GetProviderOptionsAsync(CancellationToken ct = default)
    {
        return GetAsync<IReadOnlyList<AiProviderOption>>("providers/options", ct);
    }

    /// <inheritdoc />
    public Task<Result<EnhanceContentResponse, AeroError>> EnhanceContentAsync(
        EnhanceContentRequest request,
        CancellationToken ct = default)
    {
        return PostAsync<EnhanceContentRequest, EnhanceContentResponse>("content/enhance", request, ct);
    }

    /// <inheritdoc />
    public async Task<Result<IAsyncEnumerable<EnhanceContentEvent>, AeroError>> StreamEnhanceContentAsync(
        EnhanceContentRequest request,
        CancellationToken ct = default)
    {
        HttpResponseMessage? response = null;
        try
        {
            using var message = new HttpRequestMessage(
                HttpMethod.Post,
                CreateUri("content/enhance/stream"))
            {
                Content = JsonContent.Create(request)
            };
            message.Headers.Accept.ParseAdd("text/event-stream");
            response = await client.SendAsync(
                message,
                HttpCompletionOption.ResponseHeadersRead,
                ct);

            if (ShouldUseEnhancementFallback(response.StatusCode))
            {
                response.Dispose();
                var fallback = await EnhanceContentAsync(request, ct);
                return fallback switch
                {
                    Result<EnhanceContentResponse, AeroError>.Ok ok =>
                        new Result<IAsyncEnumerable<EnhanceContentEvent>, AeroError>.Ok(
                            EnhancementRestFallback(ok.Value, ct)),
                    Result<EnhanceContentResponse, AeroError>.Failure failure => failure.Error,
                    _ => AeroError.CreateError("AI enhancement fallback failed.")
                };
            }

            if (!response.IsSuccessStatusCode)
            {
                var status = response.StatusCode;
                response.Dispose();
                return AeroError.HttpRequestError(status, "AI enhancement stream failed.");
            }

            return new Result<IAsyncEnumerable<EnhanceContentEvent>, AeroError>.Ok(
                ReadEnhancementResponseAsync(response, ct));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            response?.Dispose();
            return AeroError.CancelledError("AI enhancement stream was cancelled.");
        }
        catch (Exception)
        {
            response?.Dispose();
            return AeroError.HttpRequestError(
                HttpStatusCode.ServiceUnavailable,
                "AI enhancement stream failed.");
        }
    }

    /// <inheritdoc />
    public Task<Result<TranslateDocumentResponse, AeroError>> TranslateContentAsync(
        TranslateDocumentRequest request,
        CancellationToken ct = default)
    {
        return PostAsync<TranslateDocumentRequest, TranslateDocumentResponse>("content/translate", request, ct);
    }

    /// <inheritdoc />
    public Task<Result<GenerateContentAiTranslationResponse, AeroError>> GenerateContentTranslationAsync(
        GenerateContentAiTranslationRequest request,
        CancellationToken ct = default)
        => PostAsync<GenerateContentAiTranslationRequest, GenerateContentAiTranslationResponse>("content/localization/generate", request, ct);

    private static bool ShouldUseEnhancementFallback(HttpStatusCode statusCode)
        => statusCode is HttpStatusCode.NotFound
            or HttpStatusCode.MethodNotAllowed
            or HttpStatusCode.NotAcceptable
            or HttpStatusCode.NotImplemented;

    private static async IAsyncEnumerable<EnhanceContentEvent> ReadEnhancementResponseAsync(
        HttpResponseMessage response,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using (response)
        await using (var stream = await response.Content.ReadAsStreamAsync(cancellationToken))
        {
            await foreach (var item in AiEnhancementSseParser.ParseAsync(stream, cancellationToken))
            {
                yield return item;
            }
        }
    }

    private static async IAsyncEnumerable<EnhanceContentEvent> EnhancementRestFallback(
        EnhanceContentResponse response,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        yield return new(
            EnhanceContentEventKind.Metadata,
            Provider: response.Provider,
            Model: response.Model);
        yield return new(
            EnhanceContentEventKind.Complete,
            Response: response,
            Provider: response.Provider,
            Model: response.Model);
        await Task.CompletedTask;
    }
}

/// <summary>
/// Incrementally parses bounded AI enhancement SSE frames.
/// </summary>
public static class AiEnhancementSseParser
{
    private const int MaxEventCharacters = 1_100_000;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Parses a UTF-8 SSE response without buffering the complete HTTP body.
    /// </summary>
    public static async IAsyncEnumerable<EnhanceContentEvent> ParseAsync(
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
                    yield return Deserialize(eventName, data.ToString());
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
                if (item.Kind is EnhanceContentEventKind.Complete or EnhanceContentEventKind.Error)
                {
                    yield break;
                }

                data.Clear();
                eventName = null;
                continue;
            }

            if (line[0] == ':')
            {
                continue;
            }

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
                {
                    data.Append('\n');
                }

                data.Append(value);
                if (data.Length > MaxEventCharacters)
                {
                    throw new InvalidDataException(
                        "AI enhancement SSE event exceeded the maximum size.");
                }
            }
        }
    }

    private static EnhanceContentEvent Deserialize(string? eventName, string data)
    {
        var item = JsonSerializer.Deserialize<EnhanceContentEvent>(data, JsonOptions)
            ?? throw new InvalidDataException("AI enhancement SSE event was empty.");
        if (Enum.TryParse<EnhanceContentEventKind>(eventName, ignoreCase: true, out var kind)
            && item.Kind != kind)
        {
            item = item with { Kind = kind };
        }

        return item;
    }
}
