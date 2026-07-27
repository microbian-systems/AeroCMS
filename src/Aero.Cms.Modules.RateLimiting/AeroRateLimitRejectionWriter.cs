using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.RateLimiting;

internal static class AeroRateLimitRejectionWriter
{
    private const string CorrelationHeader = "X-Correlation-Id";

    public static async ValueTask WriteAsync(
        OnRejectedContext context,
        AeroRateLimitInfrastructureOptions options,
        CancellationToken cancellationToken)
    {
        var httpContext = context.HttpContext;
        var response = httpContext.Response;
        var correlationId = string.IsNullOrWhiteSpace(httpContext.TraceIdentifier)
            ? "rate-limit"
            : httpContext.TraceIdentifier;
        if (correlationId.Length > 128)
            correlationId = correlationId[..128];

        var policyName = httpContext.GetEndpoint()?
            .Metadata
            .GetMetadata<EnableRateLimitingAttribute>()?
            .PolicyName ?? "unknown";
        AeroRateLimitTelemetry.RecordRejected(policyName, "http");

        var logger = httpContext.RequestServices
            .GetService<ILoggerFactory>()?
            .CreateLogger(typeof(RateLimitingModule));
        logger?.LogWarning(
            "Rate limit rejected {Method} {Path} for policy {PolicyName}; correlation {CorrelationId}.",
            httpContext.Request.Method,
            httpContext.Request.Path.Value,
            policyName,
            correlationId);

        if (response.HasStarted)
            return;

        response.StatusCode = StatusCodes.Status429TooManyRequests;
        response.ContentType = "application/problem+json";
        response.Headers.CacheControl = "no-store";
        response.Headers[CorrelationHeader] = correlationId;

        int? retryAfterSeconds = null;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            retryAfterSeconds = Math.Clamp(
                (int)Math.Ceiling(retryAfter.TotalSeconds),
                1,
                options.MaximumRetryAfterSeconds);
            response.Headers.RetryAfter =
                retryAfterSeconds.Value.ToString(CultureInfo.InvariantCulture);
        }

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too many requests",
            Detail = "The request limit was exceeded. Try again later."
        };
        problem.Extensions["correlationId"] = correlationId;
        if (retryAfterSeconds.HasValue)
            problem.Extensions["retryAfterSeconds"] = retryAfterSeconds.Value;

        await response.WriteAsJsonAsync(problem, cancellationToken);
    }
}
