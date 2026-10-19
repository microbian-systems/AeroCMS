using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Modules.Identity;

/// <summary>
/// Enforces antiforgery validation for manager-cookie JSON mutation endpoints.
/// </summary>
internal sealed class ExternalIdentityAntiforgeryFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var requestAborted = context.HttpContext.RequestAborted;
        requestAborted.ThrowIfCancellationRequested();

        var validation = context.HttpContext.Features.Get<IAntiforgeryValidationFeature>();
        if (validation is { IsValid: false })
            return InvalidRequest();

        if (validation is null)
        {
            var antiforgery = context.HttpContext.RequestServices.GetRequiredService<IAntiforgery>();
            try
            {
                await antiforgery.ValidateRequestAsync(context.HttpContext);
            }
            catch (OperationCanceledException) when (requestAborted.IsCancellationRequested)
            {
                throw;
            }
            catch (AntiforgeryValidationException)
            {
                return InvalidRequest();
            }
        }

        requestAborted.ThrowIfCancellationRequested();
        return await next(context);
    }

    private static IResult InvalidRequest() => Results.BadRequest(new
    {
        message = "Antiforgery validation failed."
    });
}
