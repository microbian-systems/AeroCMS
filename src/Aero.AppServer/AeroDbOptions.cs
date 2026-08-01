using Microsoft.Extensions.Configuration;

namespace Aero.AppServer;

/// <summary>
/// Configures embedded and server-mode SurrealDB connectivity.
/// </summary>
public sealed class AeroDbOptions
{
    /// <summary>
    /// Gets or sets the embedded SurrealDB KV data path.
    /// </summary>
    public string DataPath { get; set; } = AeroAppServerConstants.SableKvDataPath;

    /// <summary>
    /// Gets or sets the SurrealDB websocket or HTTP endpoint for server mode.
    /// </summary>
    public string Endpoint { get; set; } = AeroAppServerConstants.SableEndpoint;

    /// <summary>
    /// Gets or sets the server-mode user name.
    /// </summary>
    public string Username { get; set; } = AeroAppServerConstants.SableUser;

    /// <summary>
    /// Gets or sets the server-mode password.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the SurrealDB namespace.
    /// </summary>
    public string Namespace { get; set; } = AeroAppServerConstants.SableNamespace;

    /// <summary>
    /// Gets or sets the SurrealDB database.
    /// </summary>
    public string Database { get; set; } = AeroAppServerConstants.SableDatabase;

    /// <summary>
    /// Gets the connection string for embedded mode (SurrealKV data path URI).
    /// </summary>
    public string ConnectionString
        => $"surrealkv://{DataPath}";

    /// <summary>
    /// Creates options by binding the <c>Aero:Embedded</c> configuration section over defaults.
    /// </summary>
    /// <param name="configuration">The configuration source to bind.</param>
    /// <returns>A newly bound options instance.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="configuration"/> is <see langword="null"/>.
    /// </exception>
    public static AeroDbOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var options = new AeroDbOptions();
        configuration.GetSection("Aero:Embedded").Bind(options);
        return options;
    }
}
