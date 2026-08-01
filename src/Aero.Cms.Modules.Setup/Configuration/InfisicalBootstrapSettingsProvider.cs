using Aero.Secrets.Models;
using Microsoft.Extensions.Configuration;

namespace Aero.Cms.Modules.Setup.Configuration;

/// <summary>
/// Resolves the non-secret connection settings required to create an Infisical secret manager during bootstrap.
/// </summary>
/// <remarks>
/// Environment variables take precedence over application configuration. Invalid host
/// URIs fall back to the local Infisical endpoint. Machine credentials are supplied
/// separately by the setup request and are not returned by this provider.
/// </remarks>
public sealed class InfisicalBootstrapSettingsProvider(IConfiguration configuration)
{
    /// <summary>
    /// Builds an Infisical options snapshot from environment variables and configuration.
    /// </summary>
    /// <returns>Options with stable fallbacks for host, project, environment, and secret path.</returns>
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

    /// <summary>
    /// Reads a value using environment, configuration, then fallback precedence.
    /// </summary>
    private string GetValue(string configKey, string envKey, string fallback)
        => Environment.GetEnvironmentVariable(envKey) ?? configuration[configKey] ?? fallback;
}
