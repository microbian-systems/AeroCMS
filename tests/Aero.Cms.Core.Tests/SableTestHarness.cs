using System.Linq.Expressions;
using AeroDB.Sable;
using SurrealDb.Embedded.InMemory;

namespace Aero.Cms.Core.Tests;

/// <summary>
/// Provides real in-memory SurrealDB document stores and sessions for testing.
/// Replaces NSubstitute mocks with actual SurrealDb.Embedded.InMemory.
/// Each store uses a unique namespace/database for test isolation.
/// </summary>
public sealed class SableTestHarness : IAsyncDisposable
{
    private static int _counter;

    private IDocumentStore? _store;
    private IDocumentSession? _session;

    private readonly List<Action<StoreOptions>> _configureActions = new();

    public IDocumentStore Store => _store ?? throw new InvalidOperationException("Call InitializeAsync first.");
    public IDocumentSession Session => _session ?? throw new InvalidOperationException("Call InitializeAsync first.");

    /// <summary>Register a document type with identity and SCHEMALESS mode.</summary>
    public SableTestHarness WithSchema<T>(SchemaMode? mode = null) where T : class
    {
        _configureActions.Add(o =>
        {
            var mapping = o.Schema.For<T>();

            if(mode is not null)
                mapping.SetSchemaMode(mode.Value);
            else
                mapping.SetSchemaMode(SchemaMode.Flexible);

            var idProp = typeof(T).GetProperty("Id");
            if (idProp is not null)
            {
                var param = Expression.Parameter(typeof(T), "x");
                var access = Expression.Property(param, idProp);
                var lambda = Expression.Lambda(access, param);
                typeof(DocumentMapping<>)
                    .MakeGenericType(typeof(T))
                    .GetMethod("Identity")!
                    .MakeGenericMethod(idProp.PropertyType)
                    .Invoke(mapping, [lambda]);
            }
        });
        return this;
    }

    /// <summary>Add a custom configuration action to the StoreOptions.</summary>
    public SableTestHarness WithConfiguration(Action<StoreOptions> configure)
    {
        _configureActions.Add(configure);
        return this;
    }

    /// <summary>Initializes the document store and opens a session. Must be called before using Store/Session.</summary>
    public async Task InitializeAsync()
    {
        var uniqueId = UniqueNs();
        _store = Documents.For(o =>
        {
            o.ClientFactory = () => new NoDisposeSurrealDbMemoryClient();
            o.Namespace = uniqueId;
            o.Database = uniqueId;
            foreach (var action in _configureActions)
                action(o);
        });
        await _store.InitializeAsync();
        _session = await _store.OpenSessionAsync(new SessionOptions());
    }

    /// <summary>Opens a fresh session (not tracked by this harness — you must dispose it yourself).</summary>
    public async Task<IDocumentSession> OpenSessionAsync(SessionOptions? options = null)
    {
        return await _store!.OpenSessionAsync(options ?? new SessionOptions());
    }

    public async ValueTask DisposeAsync()
    {
        if (_session is { } session)
        {
            await session.DisposeAsync();
            _session = null;
        }
        if (_store is { } store)
        {
            await store.DisposeAsync();
            _store = null;
        }
    }

    private static string UniqueNs()
    {
        var id = Interlocked.Increment(ref _counter);
        var guid = Guid.NewGuid().ToString("N")[..8];
        return $"test_{id}_{guid}";
    }
}

/// <summary>
/// Wraps SurrealDbMemoryClient and no-ops DisposeAsync, preventing the native engine
/// from being torn down during test runs. This avoids a race condition where native
/// callbacks fire after the native engine is disposed.
/// </summary>
internal sealed class NoDisposeSurrealDbMemoryClient : SurrealDbMemoryClient, IAsyncDisposable
{
    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        // Deliberately empty — native engine stays alive for the entire test run.
        // Each store uses unique namespace/database for test isolation.
    }
}
