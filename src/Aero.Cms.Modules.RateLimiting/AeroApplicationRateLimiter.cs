using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.RateLimiting;

/// <summary>
/// Identifies one server-authorized application operation for admission control.
/// </summary>
/// <remarks>
/// Callers must populate <see cref="PrincipalId"/> from authenticated server state. Raw API keys,
/// email addresses, prompts, and other secrets or PII must never be supplied.
/// </remarks>
public sealed record AeroRateLimitSubject(
    long TenantId,
    long SiteId,
    string Audience,
    string PrincipalType,
    string PrincipalId);

/// <summary>
/// Acquires named application-level permits after a protocol request has been parsed.
/// </summary>
public interface IAeroApplicationRateLimiter
{
    ValueTask<AeroRateLimitAdmissionDecision> AcquireAsync(
        string policyName,
        AeroRateLimitSubject subject,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents one application-level admission decision.
/// </summary>
public sealed record AeroRateLimitAdmissionDecision(
    bool IsAcquired,
    TimeSpan? RetryAfter);

internal sealed record AeroApplicationRateLimitPolicyDefinition(
    string PolicyName,
    AeroFixedWindowRateLimitOptions Options);

internal sealed class AeroApplicationRateLimiter : IAeroApplicationRateLimiter, IDisposable
{
    private readonly IReadOnlyDictionary<string, PartitionedRateLimiter<AeroRateLimitSubject>> _limiters;
    private readonly ILogger<AeroApplicationRateLimiter> _logger;
    private bool _disposed;

    public AeroApplicationRateLimiter(
        IEnumerable<AeroApplicationRateLimitPolicyDefinition> definitions,
        ILogger<AeroApplicationRateLimiter> logger)
    {
        _logger = logger;
        var limiters = new Dictionary<string, PartitionedRateLimiter<AeroRateLimitSubject>>(
            StringComparer.Ordinal);

        foreach (var definition in definitions)
        {
            if (!limiters.TryAdd(
                    definition.PolicyName,
                    CreateLimiter(definition)))
            {
                throw new InvalidOperationException(
                    $"Application rate-limit policy '{definition.PolicyName}' was registered more than once.");
            }
        }

        _limiters = limiters;
    }

    public async ValueTask<AeroRateLimitAdmissionDecision> AcquireAsync(
        string policyName,
        AeroRateLimitSubject subject,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);
        ArgumentNullException.ThrowIfNull(subject);

        if (subject.TenantId <= 0
            || subject.SiteId <= 0
            || string.IsNullOrWhiteSpace(subject.Audience)
            || string.IsNullOrWhiteSpace(subject.PrincipalType)
            || string.IsNullOrWhiteSpace(subject.PrincipalId))
        {
            throw new InvalidOperationException(
                "Application rate limiting requires an authenticated tenant, site, audience, and principal.");
        }

        if (!_limiters.TryGetValue(policyName, out var limiter))
        {
            _logger.LogCritical(
                "Required application rate-limit policy {PolicyName} is unavailable.",
                policyName);
            throw new InvalidOperationException(
                $"Required application rate-limit policy '{policyName}' is unavailable.");
        }

        using var lease = await limiter.AcquireAsync(subject, permitCount: 1, cancellationToken);
        if (lease.IsAcquired)
            AeroRateLimitTelemetry.RecordAccepted(policyName, "application");
        else
            AeroRateLimitTelemetry.RecordRejected(policyName, "application");

        var retryAfter = lease.TryGetMetadata(MetadataName.RetryAfter, out var retry)
            ? retry
            : (TimeSpan?)null;
        return new AeroRateLimitAdmissionDecision(lease.IsAcquired, retryAfter);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        foreach (var limiter in _limiters.Values)
            limiter.Dispose();
    }

    private static PartitionedRateLimiter<AeroRateLimitSubject> CreateLimiter(
        AeroApplicationRateLimitPolicyDefinition definition)
        => PartitionedRateLimiter.Create<AeroRateLimitSubject, string>(
            subject => RateLimitPartition.GetFixedWindowLimiter(
                CreatePartitionKey(subject, definition.PolicyName),
                _ => new FixedWindowRateLimiterOptions
                {
                    AutoReplenishment = true,
                    PermitLimit = definition.Options.PermitLimit,
                    Window = TimeSpan.FromSeconds(definition.Options.WindowSeconds),
                    QueueLimit = definition.Options.QueueLimit,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                }));

    private static string CreatePartitionKey(
        AeroRateLimitSubject subject,
        string policyName)
        => string.Join(
            '|',
            subject.TenantId.ToString(CultureInfo.InvariantCulture),
            subject.SiteId.ToString(CultureInfo.InvariantCulture),
            Normalize(subject.Audience),
            Normalize(subject.PrincipalType),
            Normalize(subject.PrincipalId),
            Normalize(policyName));

    private static string Normalize(string value)
    {
        const int maximumLength = 128;
        var normalized = value.Trim().Replace('|', '_');
        return normalized.Length <= maximumLength
            ? normalized
            : normalized[..maximumLength];
    }
}
