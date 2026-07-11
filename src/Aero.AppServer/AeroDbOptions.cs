using Microsoft.Extensions.Configuration;

namespace Aero.AppServer;

/// <summary>
/// Represents a class for AeroDbOptions.
/// </summary>
public sealed class AeroDbOptions
{
    /// <summary>
    /// Gets or sets the embedded SurrealDB KV data path.
    /// </summary>
    public string DataPath { get; set; } = AeroAppServerConstants.SableKvDataPath;

    /// <summary>
    /// Gets or sets the SurrealDB endpoint (weboscket or HTTP) for server mode.
    /// </summary>
    public string Endpoint { get; set; } = AeroAppServerConstants.SableEndpoint;

    /// <summary>
    /// Gets or sets the Username.
    /// </summary>
    public string Username { get; set; } = AeroAppServerConstants.SableUser;

    /// <summary>
    /// Gets or sets the Password.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Namespace.
    /// </summary>
    public string Namespace { get; set; } = AeroAppServerConstants.SableNamespace;

    /// <summary>
    /// Gets or sets the Database.
    /// </summary>
    public string Database { get; set; } = AeroAppServerConstants.SableDatabase;

    /// <summary>
    /// Gets the connection string for embedded mode (SurrealKV data path URI).
    /// </summary>
    public string ConnectionString
        => $"surrealkv://{DataPath}";

    /// <summary>
    /// FromConfiguration method.
    /// </summary>
    public static AeroDbOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var options = new AeroDbOptions();
        configuration.GetSection("Aero:Embedded").Bind(options);
        return options;
    }
}
