using Aero.Cms.Web.Core.Middleware;
using Microsoft.AspNetCore.Http;
using Shouldly;

namespace Aero.Cms.Core.Tests.Integration;

public sealed class RequestCancellationIsolationMiddlewareTests
{
    [Test]
    public async Task CompletedRequestDoesNotRetainCallbacksOnKestrelToken()
    {
        using var kestrelCancellation = new CancellationTokenSource();
        var context = new DefaultHttpContext
        {
            RequestAborted = kestrelCancellation.Token
        };

        var middleware = new RequestCancellationIsolationMiddleware(async httpContext =>
        {
            using var completedOperation = new CancellationTokenSource();

            // Reproduces SurrealDb.Embedded 0.10.2: the operation registers a callback
            // on RequestAborted, completes, and disposes its target without unregistering.
            httpContext.RequestAborted.Register(completedOperation.Cancel);
            await Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Should.NotThrow(kestrelCancellation.Cancel);
    }

    [Test]
    public async Task ActiveRequestStillObservesKestrelCancellation()
    {
        using var kestrelCancellation = new CancellationTokenSource();
        var context = new DefaultHttpContext
        {
            RequestAborted = kestrelCancellation.Token
        };
        var downstreamObservedCancellation = false;

        var middleware = new RequestCancellationIsolationMiddleware(httpContext =>
        {
            var completedOperation = new CancellationTokenSource();
            httpContext.RequestAborted.Register(completedOperation.Cancel);
            completedOperation.Dispose();

            Should.NotThrow(kestrelCancellation.Cancel);
            downstreamObservedCancellation = httpContext.RequestAborted.IsCancellationRequested;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        downstreamObservedCancellation.ShouldBeTrue();
    }
}
