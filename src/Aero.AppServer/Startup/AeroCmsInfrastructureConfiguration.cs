namespace Aero.AppServer.Startup;

/// <summary>
/// Defines configuration paths for deployment topology that are independent of the setup lifecycle.
/// </summary>
public static class AeroCmsInfrastructureConfiguration
{
    /// <summary>The configuration section containing database, cache, and secret-provider topology.</summary>
    public const string SectionName = "AeroCms:Infrastructure";

    /// <summary>The configured database deployment mode.</summary>
    public const string DatabaseMode = "DatabaseMode";

    /// <summary>The installation-wide SurrealDB namespace.</summary>
    public const string DatabaseNamespace = "DatabaseNamespace";

    /// <summary>The installation-wide SurrealDB database name.</summary>
    public const string DatabaseName = "DatabaseName";

    /// <summary>The configured cache deployment mode.</summary>
    public const string CacheMode = "CacheMode";

    /// <summary>The selected secret provider.</summary>
    public const string SecretProvider = "SecretProvider";
}
