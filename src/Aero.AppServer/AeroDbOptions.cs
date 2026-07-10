using Microsoft.Extensions.Configuration;

namespace Aero.AppServer;

/// <summary>
/// Represents a class for AeroDbOptions.
/// </summary>
public sealed class AeroDbOptions
{
        /// <summary>
    /// Gets or sets the Pg Version.
    /// </summary>
public string PgVersion { get; set; } = AeroAppServerConstants.PgVersion;
        /// <summary>
    /// Gets or sets the Port.
    /// </summary>
public int Port { get; set; } = AeroAppServerConstants.PgPort;
        /// <summary>
    /// Gets or sets the Username.
    /// </summary>
public string Username { get; set; } = AeroAppServerConstants.EmbeddedDbUser;
        /// <summary>
    /// Gets or sets the Password.
    /// </summary>
public string Password { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Database.
    /// </summary>
public string Database { get; set; } = AeroAppServerConstants.EmbeddedDbName;

        /// <summary>
    /// Gets or sets the Connection String.
    /// </summary>
public string ConnectionString
        =>  $"Host=localhost;Port={Port};Username={Username};Password={Password};Database={Database};";

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
