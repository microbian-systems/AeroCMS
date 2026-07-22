using Microsoft.AspNetCore.Http;

namespace Aero.Cms.Modules.Identity;

/// <summary>Bounds anonymous manager authentication starts by transport IP.</summary>
public sealed class ManagerAuthenticationRateLimiter(TimeProvider timeProvider)
{
    private const int LocalLoginPermitLimit = 5;
    private const int FederationBeginPermitLimit = 10;
    private const int Capacity = 4096;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);
    private readonly object _gate = new();
    private readonly Dictionary<PartitionKey, WindowState> _windows = [];

    public bool TryAcquireLocalLogin(HttpContext context) =>
        TryAcquire(context, AuthenticationPurpose.LocalLogin, LocalLoginPermitLimit);

    public bool TryAcquireFederationBegin(HttpContext context) =>
        TryAcquire(context, AuthenticationPurpose.FederationBegin, FederationBeginPermitLimit);

    private bool TryAcquire(HttpContext context, AuthenticationPurpose purpose, int permitLimit)
    {
        ArgumentNullException.ThrowIfNull(context);
        var key = new PartitionKey(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            purpose);
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

                if (current.PermitCount >= permitLimit)
                    return false;

                _windows[key] = current with { PermitCount = current.PermitCount + 1 };
                return true;
            }

            if (_windows.Count >= Capacity)
            {
                foreach (var expired in _windows
                             .Where(pair => now >= pair.Value.ExpiresAt)
                             .Select(pair => pair.Key)
                             .ToArray())
                {
                    _windows.Remove(expired);
                }

                if (_windows.Count >= Capacity)
                    return false;
            }

            _windows.Add(key, new WindowState(1, now.Add(Window)));
            return true;
        }
    }

    private enum AuthenticationPurpose
    {
        LocalLogin,
        FederationBegin
    }

    private readonly record struct PartitionKey(string RemoteIpAddress, AuthenticationPurpose Purpose);
    private readonly record struct WindowState(int PermitCount, DateTimeOffset ExpiresAt);
}
