namespace Aero.Cms.Abstractions.Models;

/// <summary>
/// Serializable error entry sent from the WASM client to the server
/// for logging and alerting via Wolverine.
/// Also stored in the client-side localStorage "error bucket".
/// </summary>
public sealed class ClientErrorEntry
{
        /// <summary>
    /// Gets or sets the Error Type.
    /// </summary>
public string ErrorType { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Error Message.
    /// </summary>
public string ErrorMessage { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Client Url.
    /// </summary>
public string? ClientUrl { get; set; }
        /// <summary>
    /// Gets or sets the User Agent.
    /// </summary>
public string? UserAgent { get; set; }
        /// <summary>
    /// Gets or sets the Client Timestamp.
    /// </summary>
public string? ClientTimestamp { get; set; }
        /// <summary>
    /// Gets or sets the Stack Trace.
    /// </summary>
public string? StackTrace { get; set; }
}
