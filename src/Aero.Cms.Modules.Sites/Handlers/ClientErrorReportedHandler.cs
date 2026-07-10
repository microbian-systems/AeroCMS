using Aero.Cms.Abstractions.Events;
using Microsoft.Extensions.Logging;
using Wolverine;
using Wolverine.Attributes;

namespace Aero.Cms.Modules.Sites.Handlers;

/// <summary>
/// Handles <see cref="ClientErrorReported"/> events fired when the WASM manager
/// reports an error from the client side.
///
/// Logs the error details for observability. In future, this could also:
/// - Persist errors to a database table for dashboard/reporting
/// - Send alerts via email/Slack for high-severity errors
/// - Increment error counters in OpenTelemetry metrics
/// </summary>
[WolverineHandler]
public sealed class ClientErrorReportedHandler(ILogger<ClientErrorReportedHandler> logger)
    : IWolverineHandler
{
        /// <summary>
    /// Handle method.
    /// </summary>
public Task Handle(ClientErrorReported message)
    {
        if (message.ErrorType is "HttpRequest" or "Database" or "Timeout")
        {
            logger.LogError(
                "Client error ({ErrorType}): {ErrorMessage} | URL: {Url} | UA: {UserAgent} | Time: {Timestamp}",
                message.ErrorType, message.ErrorMessage, message.ClientUrl,
                message.UserAgent, message.ClientTimestamp);
        }
        else
        {
            logger.LogWarning(
                "Client error ({ErrorType}): {ErrorMessage} | URL: {Url} | Time: {Timestamp}",
                message.ErrorType, message.ErrorMessage, message.ClientUrl,
                message.ClientTimestamp);
        }

        return Task.CompletedTask;
    }
}
