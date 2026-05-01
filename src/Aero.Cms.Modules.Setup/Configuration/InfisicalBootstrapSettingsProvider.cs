using Aero.Secrets.Models;
using Microsoft.Extensions.Configuration;

namespace Aero.Cms.Modules.Setup.Configuration;

public sealed class InfisicalBootstrapSettingsProvider(IConfiguration configuration)
{
    public InfisicalSecretManagerOptions GetSettings()
    {
        var host = GetValue("Infisical:HostUri", "INFISICAL__HOST_URI", "http://localhost:8080");
        return new InfisicalSecretManagerOptions
        {
            HostUri = Uri.TryCreate(host, UriKind.Absolute, out var uri) ? uri : new Uri("http://localhost:8080"),
            ProjectId = GetValue("Infisical:ProjectId", "INFISICAL__PROJECT_ID", string.Empty),
            EnvironmentSlug = GetValue("Infisical:EnvironmentSlug", "INFISICAL__ENVIRONMENT_SLUG", string.Empty),
            SecretPath = GetValue("Infisical:SecretPath", "INFISICAL__SECRET_PATH", "/")
        };
    }

    private string GetValue(string configKey, string envKey, string fallback)
        => Environment.GetEnvironmentVariable(envKey) ?? configuration[configKey] ?? fallback;
}
