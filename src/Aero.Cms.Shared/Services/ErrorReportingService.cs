using System.Net.Http.Json;
using System.Text.Json;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Contracts.Abstractions;
using Aero.Core;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Shared.Services;

/// <summary>
/// Client-side error reporting service.
///
/// 1. Persists the error to localStorage via <see cref="IAdminStorage"/> ("aero-error-bucket").
/// 2. Sends the error to the server via <c>POST /api/v1/admin/errors</c>,
///    where it is published as a <c>ClientErrorReported</c> Wolverine message for logging/alerting.
///
/// Uses fire-and-forget for the server call so it doesn't block the UI.
/// </summary>
public sealed class ErrorReportingService : IErrorReportingService, IDisposable
{
    private const string ErrorBucketKey = "aero-error-bucket";
    private readonly IAdminStorage _storage;
    private readonly HttpClient _httpClient;
    private readonly ILogger<ErrorReportingService> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private const int MaxStoredErrors = 50;

    public ErrorReportingService(
        IAdminStorage storage,
        HttpClient httpClient,
        ILogger<ErrorReportingService> logger)
    {
        _storage = storage;
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task ReportErrorAsync(AeroError error, string? context = null)
    {
        try
        {
            var (errorType, errorMessage) = DeconstructError(error);
            var clientUrl = context ?? "unknown";

            var report = new ClientErrorEntry
            {
                ErrorType = errorType,
                ErrorMessage = errorMessage,
                ClientUrl = clientUrl,
                UserAgent = GetUserAgent(),
                ClientTimestamp = DateTimeOffset.UtcNow.ToString("O"),
                StackTrace = Environment.StackTrace
            };

            // 1. Store locally in the "error bucket" (localStorage)
            StoreLocally(report);

            // 2. Send to server (fire-and-forget, don't block UI)
            _ = SendToServerAsync(report);
        }
        catch (Exception ex)
        {
            // Don't let error reporting itself cause errors
            _logger.LogWarning(ex, "ErrorReportingService failed to report error");
        }
    }

    private void StoreLocally(ClientErrorEntry entry)
    {
        try
        {
            var existing = _storage.GetItem<List<ClientErrorEntry>>(ErrorBucketKey) ?? [];
            existing.Add(entry);

            // Trim to max stored errors to prevent unbounded growth
            if (existing.Count > MaxStoredErrors)
                existing = existing.TakeLast(MaxStoredErrors).ToList();

            _storage.SetItem(ErrorBucketKey, existing);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to store error locally in error bucket");
        }
    }

    private async Task SendToServerAsync(ClientErrorEntry entry)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/v1/admin/errors", entry, _jsonOptions);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Server returned {StatusCode} for error report", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send error report to server");
        }
    }

    private static (string type, string message) DeconstructError(AeroError error)
    {
        return error switch
        {
            AeroError.NotFound n => ("NotFound", n.msg),
            AeroError.Validation v => ("Validation", string.Join("; ", v.Errors)),
            AeroError.HttpRequest h => ("HttpRequest", $"[{(int)h.code}] {h.msg}"),
            AeroError.Unauthorized u => ("Unauthorized", u.msg),
            AeroError.Forbidden f => ("Forbidden", f.msg),
            AeroError.Conflict c => ("Conflict", c.msg),
            AeroError.Database d => ("Database", d.msg),
            AeroError.Timeout t => ("Timeout", t.msg),
            AeroError.NotAllowed n => ("NotAllowed", n.msg),
            AeroError.BadRequest b => ("BadRequest", b.msg),
            AeroError.InvalidRequest i => ("InvalidRequest", i.msg),
            AeroError.Error e => ("Error", e.msg),
            _ => ("Unknown", error.ToString() ?? "Unknown error")
        };
    }

    private static string? GetUserAgent()
    {
        try
        {
            // In Blazor WASM, we can't access navigator.userAgent from C# directly
            // without JS interop. Return null and let the server derive it from
            // the HTTP request headers if needed.
            return null;
        }
        catch
        {
            return null;
        }
    }

    public void Dispose() { /* No managed resources to dispose */ }
}
