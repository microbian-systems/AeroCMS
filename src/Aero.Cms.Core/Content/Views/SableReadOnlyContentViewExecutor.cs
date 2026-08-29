using Aero.Cms.Abstractions.Content.Views;
using AeroDB.Sable;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aero.Cms.Core.Content.Views;

/// <summary>Configuration for a dedicated, read-only SurrealDB identity used only by content views.</summary>
public sealed class SableReadOnlyContentViewOptions
{
    public string? Endpoint { get; set; }
    public string? Namespace { get; set; }
    public string? Database { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? Token { get; set; }

    /// <summary>
    /// Explicitly permits an unauthenticated loopback endpoint for local development or testing.
    /// This option never permits anonymous access to a non-loopback endpoint.
    /// </summary>
    public bool AllowAnonymousLoopback { get; set; }

    /// <summary>Set by a host secret/configuration resolver when it supplies the dedicated store itself.</summary>
    public bool UseHostResolvedStoreFactory { get; set; }

    internal bool HasConnectionCoordinates => !string.IsNullOrWhiteSpace(Endpoint)
        && !string.IsNullOrWhiteSpace(Namespace)
        && !string.IsNullOrWhiteSpace(Database);

    internal bool HasDedicatedCredentials => !string.IsNullOrWhiteSpace(Token)
        || (!string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password));

    internal bool HasAnonymousLoopbackConfiguration => AllowAnonymousLoopback
        && HasConnectionCoordinates
        && Uri.TryCreate(Endpoint, UriKind.Absolute, out var endpoint)
        && endpoint.Scheme is "http" or "https" or "ws" or "wss"
        && endpoint.IsLoopback;

    internal bool HasExplicitDedicatedConfiguration => UseHostResolvedStoreFactory
        || HasConnectionCoordinates && (HasDedicatedCredentials || HasAnonymousLoopbackConfiguration);
}

/// <summary>Host escape hatch for encrypted-secret and external configuration resolvers.</summary>
public interface IContentViewReadOnlyStoreFactory
{
    Task<IDocumentStore> OpenAsync(CancellationToken ct = default);
}

/// <summary>Private key preventing content-view reads from resolving the application's unkeyed store.</summary>
public static class ContentViewReadOnlyStoreKey
{
    public static readonly object Value = new();
}

public static class SableReadOnlyContentViewServiceCollectionExtensions
{
    /// <summary>
    /// Opts a host into query-backed views with a separate Sable store. Credentials must name a
    /// SurrealDB database user/token restricted to SELECT. This registration never uses the
    /// application's unkeyed <see cref="IDocumentStore"/>.
    /// </summary>
    public static IServiceCollection AddSableReadOnlyContentViews(
        this IServiceCollection services,
        Action<SableReadOnlyContentViewOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        var options = new SableReadOnlyContentViewOptions();
        configure(options);
        services.AddSingleton(options);
        if (!options.UseHostResolvedStoreFactory
            && options.HasConnectionCoordinates
            && options.HasDedicatedCredentials)
        {
            services.AddKeyedSingleton<IDocumentStore>(ContentViewReadOnlyStoreKey.Value, (provider, _) =>
            {
                var storeOptions = new StoreOptions
                {
                    Endpoint = options.Endpoint!, Namespace = options.Namespace!, Database = options.Database!,
                    Username = options.Username, Password = options.Password, Token = options.Token,
                    ServiceProvider = provider,
                    LoggerFactory = provider.GetService<Microsoft.Extensions.Logging.ILoggerFactory>(),
                    Schema = { AutoCreate = false }
                };
                var store = new DocumentStore(storeOptions);
                store.InitializeAsync().GetAwaiter().GetResult();
                return store;
            });
        }
        services.TryAddSingleton<SurrealHttpBoundedQueryTransport>();
        services.TryAddSingleton<IContentViewBoundedQueryTransport>(provider =>
            provider.GetRequiredService<SurrealHttpBoundedQueryTransport>());
        services.Replace(ServiceDescriptor.Singleton<IAdminReadOnlyContentViewExecutor>(provider =>
            provider.GetRequiredService<SurrealHttpBoundedQueryTransport>()));
        services.Replace(ServiceDescriptor.Singleton<IReadOnlyContentViewExecutor, SableReadOnlyContentViewExecutor>());
        return services;
    }
}

/// <summary>
/// Uses a host-owned bounded transport with the dedicated read-only identity.  Sable's public
/// <c>RawQueryAsync</c> and <c>StreamAsync</c> both materialize a <c>List&lt;T&gt;</c> before callers can
/// enforce a byte ceiling, so this executor deliberately does not call either API.
/// </summary>
public sealed class SableReadOnlyContentViewExecutor(
    IServiceProvider services,
    SableReadOnlyContentViewOptions options,
    IContentViewSourceRegistry sources) : IReadOnlyContentViewExecutor
{
    private IContentViewBoundedQueryTransport? Transport => services.GetService<IContentViewBoundedQueryTransport>();

    public bool IsReadOnlyGuaranteed => options.HasExplicitDedicatedConfiguration
        && sources.IsValid
        && sources.HasSources
        && Transport is { EnforcesLimitsBeforeMaterialization: true };

    public async Task<ContentViewExecutionResult> ExecuteAsync(ContentViewExecutionRequest request, CancellationToken ct = default)
    {
        var transport = Transport;
        if (!IsReadOnlyGuaranteed || transport is null)
            throw new InvalidOperationException("A dedicated read-only identity and host-owned pre-materialization bounded transport are required.");
        return await transport.ExecuteBoundedAsync(request, ct);
    }
}
