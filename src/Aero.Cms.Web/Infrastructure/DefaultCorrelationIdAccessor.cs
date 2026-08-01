using Aero.Core.Http;

namespace Aero.Cms.Web.Infrastructure;

/// <summary>
/// Reads the current request correlation identifier from HTTP request or response headers.
/// </summary>
public sealed class DefaultCorrelationIdAccessor : ICorrelationIdAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private const string CorrelationIdHeader = "X-Correlation-Id";

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultCorrelationIdAccessor"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">Provides access to the current request context.</param>
public DefaultCorrelationIdAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Gets the correlation identifier supplied on the request or added to the response.
    /// </summary>
    /// <value>
    /// The first header value, preferring the request header, or <see langword="null"/> outside
    /// an HTTP request or when neither header is present.
    /// </value>
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
