using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Aero.Cms.Modules.RateLimiting;

/// <summary>
/// Configures one partitioned fixed-window policy.
/// </summary>
public sealed class AeroFixedWindowRateLimitOptions
{
    public int PermitLimit { get; set; }
    public int WindowSeconds { get; set; }
    public int QueueLimit { get; set; }
}

/// <summary>
/// Configures one partitioned concurrency policy.
/// </summary>
public sealed class AeroConcurrencyRateLimitOptions
{
    public int PermitLimit { get; set; }
    public int QueueLimit { get; set; }
}

/// <summary>
/// Configures shared rejection-response behavior.
/// </summary>
public sealed class AeroRateLimitInfrastructureOptions
{
    public int MaximumRetryAfterSeconds { get; set; } = 3600;
}

/// <summary>
/// Reads and validates AeroCMS rate-limiting configuration.
/// </summary>
public static class AeroRateLimitConfiguration
{
    public const string SectionName = "AeroCms:RateLimiting";
    public const string PoliciesSectionName = SectionName + ":Policies";

    public static AeroFixedWindowRateLimitOptions ReadFixedWindowOptions(
        IConfiguration? configuration,
        string configurationName,
        AeroFixedWindowRateLimitOptions defaults,
        string policyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationName);
        ArgumentNullException.ThrowIfNull(defaults);
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);

        var options = new AeroFixedWindowRateLimitOptions
        {
            PermitLimit = defaults.PermitLimit,
            WindowSeconds = defaults.WindowSeconds,
            QueueLimit = defaults.QueueLimit
        };

        configuration?.GetSection($"{PoliciesSectionName}:{configurationName}").Bind(options);

        var failures = new List<string>();
        if (options.PermitLimit is < 1 or > 1_000_000)
            failures.Add("PermitLimit must be between 1 and 1,000,000.");
        if (options.WindowSeconds is < 1 or > 86_400)
            failures.Add("WindowSeconds must be between 1 and 86,400.");
        if (options.QueueLimit is < 0 or > 1_000)
            failures.Add("QueueLimit must be between 0 and 1,000.");

        ThrowIfInvalid(policyName, typeof(AeroFixedWindowRateLimitOptions), failures);
        return options;
    }

    public static AeroConcurrencyRateLimitOptions ReadConcurrencyOptions(
        IConfiguration? configuration,
        string configurationName,
        AeroConcurrencyRateLimitOptions defaults,
        string policyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationName);
        ArgumentNullException.ThrowIfNull(defaults);
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);

        var options = new AeroConcurrencyRateLimitOptions
        {
            PermitLimit = defaults.PermitLimit,
            QueueLimit = defaults.QueueLimit
        };

        configuration?.GetSection($"{PoliciesSectionName}:{configurationName}").Bind(options);

        var failures = new List<string>();
        if (options.PermitLimit is < 1 or > 10_000)
            failures.Add("PermitLimit must be between 1 and 10,000.");
        if (options.QueueLimit is < 0 or > 1_000)
            failures.Add("QueueLimit must be between 0 and 1,000.");

        ThrowIfInvalid(policyName, typeof(AeroConcurrencyRateLimitOptions), failures);
        return options;
    }

    public static AeroRateLimitInfrastructureOptions ReadInfrastructureOptions(
        IConfiguration? configuration)
    {
        var options = new AeroRateLimitInfrastructureOptions();
        configuration?.GetSection(SectionName).Bind(options);

        var failures = new List<string>();
        if (options.MaximumRetryAfterSeconds is < 1 or > 86_400)
            failures.Add("MaximumRetryAfterSeconds must be between 1 and 86,400.");

        ThrowIfInvalid(
            SectionName,
            typeof(AeroRateLimitInfrastructureOptions),
            failures);
        return options;
    }

    private static void ThrowIfInvalid(
        string name,
        Type optionsType,
        IReadOnlyCollection<string> failures)
    {
        if (failures.Count > 0)
            throw new OptionsValidationException(name, optionsType, failures);
    }
}
