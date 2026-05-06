namespace Aero.Cms.Abstractions.Events;

/// <summary>
/// Wolverine event published when a client-side error is reported from the manager UI.
/// Fired on the server after receiving an error report from the WASM client.
/// </summary>
public sealed record ClientErrorReported(
    string ErrorType,        // e.g. "NotFound", "Validation", "HttpRequest", "Generic"
    string ErrorMessage,     // Human-readable error description
    string? ClientUrl,       // The page/route where the error occurred
    string? UserAgent,       // Browser user agent
    string? ClientTimestamp, // ISO 8601 timestamp from the client
    string? StackTrace       // Optional stack trace
);
