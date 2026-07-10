using Aero.Core.Http;

namespace Aero.Cms.Web.Infrastructure;

/// <summary>
/// Default implementation of ICorrelationIdAccessor using IHttpContextAccessor.
/// </summary>
public sealed class DefaultCorrelationIdAccessor : ICorrelationIdAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private const string CorrelationIdHeader = "X-Correlation-Id";

        /// <summary>
    /// Initializes a new instance of the <see cref="DefaultCorrelationIdAccessor"/> class.
    /// </summary>
public DefaultCorrelationIdAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

        /// <summary>
    /// Gets or sets the Correlation Id.
    /// </summary>
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
