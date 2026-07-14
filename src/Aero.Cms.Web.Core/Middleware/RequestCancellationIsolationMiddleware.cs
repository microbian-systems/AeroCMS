using Aero.Cms.Web.Core.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aero.Cms.Web.Core.Middleware;

/// <summary>
/// Isolates the server-owned request cancellation token from callbacks registered by
/// downstream dependencies. Cancellation still flows into the request while it is active,
/// but callbacks retained after the request pipeline completes cannot run when Kestrel
/// later finalizes its token. This contains the cancellation-registration leak in the
/// SurrealDb.Embedded 0.10.2 request engine while preserving request cancellation.
/// </summary>
public sealed class RequestCancellationIsolationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestCancellationIsolationMiddleware> _logger;

    /// <summary>
    /// Creates the request cancellation boundary.
    /// </summary>
    public RequestCancellationIsolationMiddleware(
        RequestDelegate next,
        ILogger<RequestCancellationIsolationMiddleware>? logger = null)
    {
        _next = next;
        _logger = logger ?? NullLogger<RequestCancellationIsolationMiddleware>.Instance;
    }

    /// <summary>
    /// Executes the request with a linked, request-scoped cancellation token.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var serverCancellation = context.RequestAborted;
        using var requestCancellation = new CancellationTokenSource();
        using var serverCancellationRegistration = serverCancellation.Register(() =>
        {
            try
            {
                requestCancellation.Cancel();
            }
            catch (Exception exception)
            {
                var rootCauses = ExceptionDiagnostics.GetRootCauses(exception);
                _logger.LogWarning(
                    exception,
                    "Suppressed {RootCauseCount} exception(s) from downstream RequestAborted callbacks while cancelling {RequestPath}",
                    rootCauses.Count,
                    context.Request.Path);

                for (var index = 0; index < rootCauses.Count; index++)
                {
                    var rootCause = rootCauses[index];
                    _logger.LogDebug(
                        rootCause,
                        "Request cancellation callback root cause {RootCauseIndex}/{RootCauseCount}: {ExceptionType}: {ExceptionMessage}",
                        index + 1,
                        rootCauses.Count,
                        rootCause.GetType().FullName,
                        rootCause.Message);
                }
            }
        });

        context.RequestAborted = requestCancellation.Token;

        try
        {
            await _next(context).ConfigureAwait(false);
        }
        finally
        {
            context.RequestAborted = serverCancellation;
        }
    }
}
