using Microsoft.AspNetCore.Http;

namespace Aero.Cms.Modules.Identity;

/// <summary>Bounded, fail-closed limiter for manager-issued local member resets.</summary>
public sealed class ManagerLocalPasswordResetRateLimiter(TimeProvider timeProvider)
{
    private const int PermitLimit = 5;
    private const int Capacity = 4096;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);
    private readonly object _gate = new();
    private readonly Dictionary<PartitionKey, WindowState> _windows = [];

    public bool TryAcquire(HttpContext context, long tenantId, long siteId)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (tenantId <= 0 || siteId <= 0)
            return false;

        var key = new PartitionKey(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            tenantId,
            siteId);
        var now = timeProvider.GetUtcNow();
        lock (_gate)
        {
            if (_windows.TryGetValue(key, out var current))
            {
                if (now >= current.ExpiresAt)
                {
                    _windows[key] = new WindowState(1, now.Add(Window));
                    return true;
                }
                if (current.PermitCount >= PermitLimit)
                    return false;
                _windows[key] = current with { PermitCount = current.PermitCount + 1 };
                return true;
            }

            if (_windows.Count >= Capacity)
            {
                foreach (var expired in _windows.Where(pair => now >= pair.Value.ExpiresAt)
                             .Select(pair => pair.Key).ToArray())
                    _windows.Remove(expired);
                if (_windows.Count >= Capacity)
                    return false;
            }

            _windows.Add(key, new WindowState(1, now.Add(Window)));
            return true;
        }
    }

    private readonly record struct PartitionKey(string RemoteIpAddress, long TenantId, long SiteId);
    private readonly record struct WindowState(int PermitCount, DateTimeOffset ExpiresAt);
}
