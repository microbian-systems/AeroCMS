using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Aero.Cms.Modules.Setup;

/// <summary>
/// Represents a class for SetupGateMiddleware.
/// </summary>
public sealed class SetupGateMiddleware(
    ISetupInitializationService setupInitializationService,
    SetupPathAllowlist allowlist) : IMiddleware
{
        /// <summary>
    /// InvokeAsync method.
    /// </summary>
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
/// Represents a class for SetupApplicationBuilderExtensions.
/// </summary>
public static class SetupApplicationBuilderExtensions
{
        /// <summary>
    /// UseCmsSetupGate method.
    /// </summary>
public static IApplicationBuilder UseCmsSetupGate(this IApplicationBuilder app)
        => app.UseMiddleware<SetupGateMiddleware>();
}
