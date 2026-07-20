using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Aero.Cms.Modules.Setup;

/// <summary>
/// Prevents access to the normal application until bootstrap configuration is available.
/// </summary>
/// <remarks>
/// Allowlisted paths and requests made after setup is considered complete continue through
/// the pipeline. Other GET and HEAD requests receive a temporary redirect to <c>/setup</c>;
/// non-idempotent methods receive 404 so request bodies are not replayed against the setup route.
/// </remarks>
public sealed class SetupGateMiddleware(
    ISetupInitializationService setupInitializationService,
    SetupPathAllowlist allowlist) : IMiddleware
{
    /// <inheritdoc />
public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (allowlist.IsAllowed(context.Request.Path) || await setupInitializationService.IsSetupCompleteAsync(context.RequestAborted))
        {
            await next(context);
            return;
        }

        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        context.Response.StatusCode = StatusCodes.Status307TemporaryRedirect;
        context.Response.Headers.Location = SetupPathAllowlist.SetupPath;
    }
}

/// <summary>
/// Adds the CMS setup access gate to an ASP.NET Core pipeline.
/// </summary>
public static class SetupApplicationBuilderExtensions
{
    /// <summary>
    /// Registers <see cref="SetupGateMiddleware"/> at the current pipeline position.
    /// </summary>
    /// <param name="app">The application builder to modify.</param>
    /// <returns>The application builder for continued pipeline configuration.</returns>
public static IApplicationBuilder UseCmsSetupGate(this IApplicationBuilder app)
        => app.UseMiddleware<SetupGateMiddleware>();
}
