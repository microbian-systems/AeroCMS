namespace Aero.Cms.Abstractions.Models;

/// <summary>
/// Serializable error entry sent from the WASM client to the server
/// for logging and alerting via Wolverine.
/// Also stored in the client-side localStorage "error bucket".
/// </summary>
public sealed class ClientErrorEntry
{
    public string ErrorType { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public string? ClientUrl { get; set; }
    public string? UserAgent { get; set; }
    public string? ClientTimestamp { get; set; }
    public string? StackTrace { get; set; }
}
