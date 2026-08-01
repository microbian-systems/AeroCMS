using Aero.Cms.Abstractions.Events;
using Microsoft.Extensions.Logging;
using Wolverine;
using Wolverine.Attributes;

namespace Aero.Cms.Modules.Sites.Handlers;

/// <summary>
/// Writes manager-reported client failures to the server log.
/// </summary>
/// <param name="logger">The structured logger receiving client error details.</param>
/// <remarks>
/// HTTP-request, database, and timeout reports are logged as errors; all other error types are
/// logged as warnings. The handler performs no persistence, retry, or notification work.
/// </remarks>
[WolverineHandler]
public sealed class ClientErrorReportedHandler(ILogger<ClientErrorReportedHandler> logger)
    : IWolverineHandler
{
    /// <summary>
    /// Records a client-side error report at a severity derived from its error type.
    /// </summary>
    /// <param name="message">The client-supplied error details to record.</param>
    /// <returns>An already-completed task after the log entry is emitted.</returns>
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
