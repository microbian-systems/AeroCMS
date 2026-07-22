using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Modules.Setup.Bootstrap;

/// <summary>
/// Coordinates request admission while a configured bootstrap is initializing runtime services.
/// </summary>
public sealed class RuntimeBootstrapReadinessGate
{
    private readonly TaskCompletionSource<bool> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Initializes the gate from the immutable bootstrap mode observed while composing the process.
    /// </summary>
    /// <param name="requiresReadiness">Whether this process started in configured mode.</param>
    public RuntimeBootstrapReadinessGate(bool requiresReadiness)
    {
        RequiresReadiness = requiresReadiness;
        if (!requiresReadiness)
        {
            _completion.TrySetResult(true);
        }
    }

    /// <summary>Gets whether this process must hold ordinary requests until runtime initialization completes.</summary>
    public bool RequiresReadiness { get; }

    /// <summary>Signals that runtime initialization completed and waiting requests may continue.</summary>
    public void SignalReady() => _completion.TrySetResult(true);

    /// <summary>Signals that runtime initialization failed and waiting requests must be rejected.</summary>
    public void SignalFailure() => _completion.TrySetResult(false);

    /// <summary>Waits for the terminal runtime-initialization result.</summary>
    public Task<bool> WaitAsync(CancellationToken cancellationToken)
        => _completion.Task.WaitAsync(cancellationToken);
}

/// <summary>
/// Applies configured-bootstrap readiness before site resolution can inspect ordinary requests.
/// </summary>
public sealed class RuntimeBootstrapReadinessMiddleware(
    SetupPathAllowlist allowlist,
    RuntimeBootstrapReadinessGate readinessGate) : IMiddleware
{
    /// <inheritdoc />
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (!allowlist.IsAllowed(context.Request.Path) &&
            readinessGate.RequiresReadiness &&
            !await readinessGate.WaitAsync(context.RequestAborted))
        {
            var statusCodePages = context.Features.Get<IStatusCodePagesFeature>();
            if (statusCodePages is not null)
            {
                statusCodePages.Enabled = false;
            }

            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync("Service Unavailable", context.RequestAborted);
            return;
        }

        await next(context);
    }
}

/// <summary>
/// Adds the configured-bootstrap readiness boundary before later startup filters such as site resolution.
/// </summary>
public sealed class RuntimeBootstrapReadinessStartupFilter : IStartupFilter
{
    /// <inheritdoc />
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        => app =>
        {
            app.UseMiddleware<RuntimeBootstrapReadinessMiddleware>();
            next(app);
        };
}

/// <summary>
/// Finalizes startup-filter registration after all modules have contributed their filters.
/// </summary>
public static class RuntimeBootstrapReadinessStartupFilterOrdering
{
    /// <summary>
    /// Moves the configured-bootstrap readiness filter to the outermost startup-filter position.
    /// </summary>
    /// <param name="services">The completed application service collection.</param>
    /// <exception cref="InvalidOperationException">The readiness filter was not registered.</exception>
    public static void MoveReadinessFilterToStart(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var descriptor = services.FirstOrDefault(candidate =>
            candidate.ServiceType == typeof(IStartupFilter) &&
            candidate.ImplementationType == typeof(RuntimeBootstrapReadinessStartupFilter));
        if (descriptor is null)
        {
            throw new InvalidOperationException("The configured-bootstrap readiness startup filter is not registered.");
        }

        services.Remove(descriptor);
        services.Insert(0, descriptor);
    }
}
