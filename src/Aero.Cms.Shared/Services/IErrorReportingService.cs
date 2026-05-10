using Aero.Core;

namespace Aero.Cms.Shared.Services;

/// <summary>
/// Service for capturing manager UI errors:
/// 1. Persists to client-side localStorage "error" bucket.
/// 2. Sends error details to the server for Wolverine-based logging/alerting.
/// </summary>
public interface IErrorReportingService
{
    /// <summary>
    /// Reports an error that occurred in the manager UI.
    /// </summary>
    /// <param name="error">The AeroError to report.</param>
    /// <param name="context">Optional context string (e.g. current page route, operation name).</param>
    Task ReportErrorAsync(AeroError error, string? context = null);
}
