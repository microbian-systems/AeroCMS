using Aero.Cms.Core;
using Aero.Core.Http;
using Aero.Models.Entities;
using Aero.Modular;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WorkOS;

namespace Aero.Cms.Modules.WorkOS;

/// <summary>
/// Integrates the WorkOS SDK client with the Aero module service collection.
/// </summary>
/// <remarks>
/// Configuration reads the WorkOS API key from <c>WorkOs:ApiKey</c>. A missing key or
/// client-construction failure is logged rather than preventing host startup.
/// </remarks>
[Module(nameof(WorkOsModule))]
public class WorkOsModule : AeroModuleBase
{
        /// <inheritdoc />
public override string Name => nameof(WorkOsModule);

        /// <inheritdoc />
public override string Version => AeroConstants.Version;

        /// <inheritdoc />
public override string Author => AeroConstants.Author;

        /// <inheritdoc />
public override IReadOnlyList<string> Dependencies => [];

        /// <inheritdoc />
public override IReadOnlyList<string> Category => [];

        /// <inheritdoc />
public override IReadOnlyList<string> Tags => [];

        /// <summary>
    /// Creates and registers a singleton WorkOS client from module configuration.
    /// </summary>
    /// <param name="services">The collection that receives the client when construction succeeds.</param>
    /// <param name="config">Configuration containing the optional <c>WorkOs:ApiKey</c> value.</param>
    /// <param name="env">The host environment; not used by this module.</param>
    /// <remarks>
    /// This method allocates the SDK's <see cref="HttpClient"/> directly. It catches and logs
    /// client-construction failures, so a failed registration is observable only through logging
    /// and later service resolution.
    /// </remarks>
public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        var apiKey = config?.GetValue<string>("WorkOs:ApiKey");
        if (string.IsNullOrEmpty(apiKey))
            log.Warning("WorkOS API key not found in configuration. WorkOS module will not be fully configured.");
        //Workos.SetApiKey(apiKey ?? "my-super-secret-key");

        var opts = new WorkOSOptions()
        {
            ApiKey = apiKey,
            HttpClient = new HttpClient() // todo - should we setup workos client w/ our AeroHttpClient?
        };

        try
        {
            var client = new WorkOSClient(opts);
            services.AddSingleton(client);
            // https://github.com/workos/workos-dotnet
            //Wos.WorkOSClient = client;
        }
        catch(Exception ex)
        {
            log.Warning($"WorkOS Error: {ex.Message}");
        }
    }
}


/// <summary>
/// Provides experimental WorkOS directory-user operations.
/// </summary>
/// <param name="client">The SDK client used to issue raw WorkOS requests.</param>
public sealed class WorkOsService(WorkOSClient client)
{
        /// <summary>
    /// Sends a raw directory-user request and then reports that user creation is not implemented.
    /// </summary>
    /// <param name="user">The Aero user intended for creation; currently not included in the request.</param>
    /// <returns>A task that never completes successfully.</returns>
    /// <exception cref="NotImplementedException">
    /// Always thrown after the raw WorkOS request completes successfully.
    /// </exception>
    /// <remarks>
    /// SDK or transport exceptions propagate before <see cref="NotImplementedException"/> is thrown.
    /// Callers must not treat the raw request as a completed user-provisioning contract.
    /// </remarks>
public async Task AddUser(AeroUser user)
    {
        var opts = new BaseOptions();
        var request = new WorkOSRequest
        {
            Method = HttpMethod.Post,
            Path = "/directory_users",
            Options = opts
        };

        await client.MakeRawAPIRequest(request);
        throw new NotImplementedException();
    }
}


/// <summary>
/// Adapts an externally configured <see cref="HttpClient"/> to Aero's HTTP client base.
/// </summary>
public sealed class WorkOsHttpClient : HttpClientBase
{
        /// <summary>
    /// Initializes a WorkOS HTTP adapter with the supplied transport and logger.
    /// </summary>
    /// <param name="httpClient">The transport owned and configured by dependency injection.</param>
    /// <param name="logger">The logger used by the base HTTP client.</param>
public WorkOsHttpClient(HttpClient httpClient, ILogger<HttpClientBase> logger)
        : base(httpClient, logger)
    {
    }
}