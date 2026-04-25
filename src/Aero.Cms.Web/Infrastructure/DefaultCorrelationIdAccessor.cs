using Aero.Core.Http;
using Microsoft.AspNetCore.Http;

namespace Aero.Cms.Web.Infrastructure;

/// <summary>
/// Default implementation of ICorrelationIdAccessor using IHttpContextAccessor.
/// </summary>
public sealed class DefaultCorrelationIdAccessor : ICorrelationIdAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private const string CorrelationIdHeader = "X-Correlation-Id";

    public DefaultCorrelationIdAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? CorrelationId
    {
        get
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return null;

            if (context.Request.Headers.TryGetValue(CorrelationIdHeader, out var correlationId))
            {
                return correlationId;
            }

            if (context.Response.Headers.TryGetValue(CorrelationIdHeader, out correlationId))
            {
                return correlationId;
            }

            return null;
        }
    }
}
