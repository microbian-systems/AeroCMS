using System.Diagnostics.Metrics;

namespace Aero.Cms.Modules.RateLimiting;

internal static class AeroRateLimitTelemetry
{
    public const string MeterName = "Aero.Cms.RateLimiting";

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> Accepted =
        Meter.CreateCounter<long>("aero.rate_limit.accepted");
    private static readonly Counter<long> Rejected =
        Meter.CreateCounter<long>("aero.rate_limit.rejected");

    public static void RecordAccepted(string policyName, string layer)
        => Accepted.Add(
            1,
            new KeyValuePair<string, object?>("policy", policyName),
            new KeyValuePair<string, object?>("layer", layer));

    public static void RecordRejected(string policyName, string layer)
        => Rejected.Add(
            1,
            new KeyValuePair<string, object?>("policy", policyName),
            new KeyValuePair<string, object?>("layer", layer));
}
