namespace Aero.AppServer;

/// <summary>
/// Represents a class for AeroAppServerConstants.
/// </summary>
public static class AeroAppServerConstants
{
        /// <summary>
        /// CacheName.
        /// </summary>
        public const string CacheName = "aero-cache";
        /// <summary>
        /// CacheUrl.
        /// </summary>
        public const string CacheUrl = "localhost:33333";
        /// <summary>
        /// CacheHost.
        /// </summary>
        public const string CacheHost = "localhost";
        /// <summary>
        /// CachePort.
        /// </summary>
        public const int CachePort = 33333;

        // SurrealDB embedded (Sable) defaults
        /// <summary>
        /// Default data path for SurrealDB KV embedded store.
        /// </summary>
        public const string SableKvDataPath = "App_Data/aerodb-surrealkv";
        /// <summary>
        /// Default SurrealDB server endpoint (websocket).
        /// </summary>
        public const string SableEndpoint = "ws://localhost:8000/rpc";
        /// <summary>
        /// Default SurrealDB server username.
        /// </summary>
        public const string SableUser = "root";
        /// <summary>
        /// Default SurrealDB namespace.
        /// </summary>
        public const string SableNamespace = "aero";
        /// <summary>
        /// Default SurrealDB database name.
        /// </summary>
        public const string SableDatabase = "aero";
}
