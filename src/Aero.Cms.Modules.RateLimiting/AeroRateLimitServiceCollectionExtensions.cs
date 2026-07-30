using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aero.Cms.Modules.RateLimiting;

/// <summary>
/// Registers configuration-backed named rate-limiter policies for AeroCMS feature modules.
/// </summary>
public static class AeroRateLimitServiceCollectionExtensions
{
    public static IServiceCollection AddAeroApplicationFixedWindowRateLimitPolicy(
        this IServiceCollection services,
        IConfiguration? configuration,
        string policyName,
        string configurationName,
        AeroFixedWindowRateLimitOptions defaults)
    {
        ArgumentNullException.ThrowIfNull(services);
        var policyOptions = AeroRateLimitConfiguration.ReadFixedWindowOptions(
            configuration,
            configurationName,
            defaults,
            policyName);

        services.AddSingleton(
            new AeroApplicationRateLimitPolicyDefinition(policyName, policyOptions));
        services.TryAddSingleton<IAeroApplicationRateLimiter, AeroApplicationRateLimiter>();
        return services;
    }

    public static IServiceCollection AddAeroFixedWindowRateLimitPolicy(
        this IServiceCollection services,
        IConfiguration? configuration,
        string policyName,
        string configurationName,
        AeroFixedWindowRateLimitOptions defaults)
    {
        ArgumentNullException.ThrowIfNull(services);
        var policyOptions = AeroRateLimitConfiguration.ReadFixedWindowOptions(
            configuration,
            configurationName,
            defaults,
            policyName);

        services.AddRateLimiter(options =>
        {
            options.AddPolicy(policyName, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    AeroRateLimitPartitionKeyFactory.Create(httpContext, policyName),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = policyOptions.PermitLimit,
                        Window = TimeSpan.FromSeconds(policyOptions.WindowSeconds),
                        QueueLimit = policyOptions.QueueLimit,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                    }));
        });

        return services;
    }

    public static IServiceCollection AddAeroConcurrencyRateLimitPolicy(
        this IServiceCollection services,
        IConfiguration? configuration,
        string policyName,
        string configurationName,
        AeroConcurrencyRateLimitOptions defaults)
    {
        ArgumentNullException.ThrowIfNull(services);
        var policyOptions = AeroRateLimitConfiguration.ReadConcurrencyOptions(
            configuration,
            configurationName,
            defaults,
            policyName);

        services.AddRateLimiter(options =>
        {
            options.AddPolicy(policyName, httpContext =>
                RateLimitPartition.GetConcurrencyLimiter(
                    AeroRateLimitPartitionKeyFactory.Create(httpContext, policyName),
                    _ => new ConcurrencyLimiterOptions
                    {
                        PermitLimit = policyOptions.PermitLimit,
                        QueueLimit = policyOptions.QueueLimit,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                    }));
        });

        return services;
    }
}
