using Microsoft.Extensions.Configuration;

namespace Aero.AppServer;

public sealed class AeroDbOptions
{
    public string PgVersion { get; set; } = AeroAppServerConstants.PgVersion;
    public int Port { get; set; } = AeroAppServerConstants.PgPort;
    public string Username { get; set; } = AeroAppServerConstants.EmbeddedDbUser;
    public string Password { get; set; } = string.Empty;
    public string Database { get; set; } = AeroAppServerConstants.EmbeddedDbName;

    public string ConnectionString
        =>  $"Host=localhost;Port={Port};Username={Username};Password={Password};Database={Database};";

    public static AeroDbOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var options = new AeroDbOptions();
        configuration.GetSection("Aero:Embedded").Bind(options);
        return options;
    }
}
