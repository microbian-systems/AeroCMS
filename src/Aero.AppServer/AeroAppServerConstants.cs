namespace Aero.AppServer;

/// <summary>
/// Defines the infrastructure names and local-development defaults used by the application server.
/// </summary>
public static class AeroAppServerConstants
{
        /// <summary>
        /// Identifies cache mode that hosts Garnet in the application process.
        /// </summary>
        public const string LocalCacheMode = "Local";
        /// <summary>
        /// Identifies cache mode that connects to a remote Redis-compatible endpoint.
        /// </summary>
        public const string ServerCacheMode = "Server";
        /// <summary>
        /// Gets the logical cache resource name.
        /// </summary>
        public const string CacheName = "aero-cache";
        /// <summary>
        /// Gets the default local Garnet endpoint in host-and-port form.
        /// </summary>
        public const string CacheUrl = "localhost:33333";
        /// <summary>
        /// Gets the loopback host used by the local Garnet readiness probe.
        /// </summary>
        public const string CacheHost = "localhost";
        /// <summary>
        /// Gets the TCP port used by the in-process Garnet server.
        /// </summary>
        public const int CachePort = 33333;

        // SurrealDB embedded (Sable) defaults
        /// <summary>
        /// Gets the default content-root-relative path for the embedded SurrealKV store.
        /// </summary>
        public const string SableKvDataPath = "App_Data/aerodb-surrealkv";
        /// <summary>
        /// Gets the default SurrealDB websocket RPC endpoint for server mode.
        /// </summary>
        public const string SableEndpoint = "ws://localhost:8000/rpc";
        /// <summary>
        /// Gets the default SurrealDB server user name.
        /// </summary>
        public const string SableUser = "root";
        /// <summary>
        /// Gets the default SurrealDB namespace.
        /// </summary>
        public const string SableNamespace = "aero";
        /// <summary>
        /// Gets the default SurrealDB database name.
        /// </summary>
        public const string SableDatabase = "aero";
}
