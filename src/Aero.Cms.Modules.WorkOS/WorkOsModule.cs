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
/// Represents a class for WorkOsModule.
/// </summary>
[Module(nameof(WorkOsModule))]
public class WorkOsModule : AeroModuleBase
{
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public override string Name => nameof(WorkOsModule);

        /// <summary>
    /// Gets or sets the Version.
    /// </summary>
public override string Version => AeroConstants.Version;

        /// <summary>
    /// Gets or sets the Author.
    /// </summary>
public override string Author => AeroConstants.Author;

        /// <summary>
    /// Gets or sets the Dependencies.
    /// </summary>
public override IReadOnlyList<string> Dependencies => [];

        /// <summary>
    /// Gets or sets the Category.
    /// </summary>
public override IReadOnlyList<string> Category => [];

        /// <summary>
    /// Gets or sets the Tags.
    /// </summary>
public override IReadOnlyList<string> Tags => [];

        /// <summary>
    /// ConfigureServices method.
    /// </summary>
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
/// Represents a class for WorkOsService.
/// </summary>
public sealed class WorkOsService(WorkOSClient client)
{
        /// <summary>
    /// AddUser method.
    /// </summary>
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
/// Represents a class for WorkOsHttpClient.
/// </summary>
public sealed class WorkOsHttpClient : HttpClientBase
{
        /// <summary>
    /// Initializes a new instance of the <see cref="WorkOsHttpClient"/> class.
    /// </summary>
public WorkOsHttpClient(HttpClient httpClient, ILogger<HttpClientBase> logger)
        : base(httpClient, logger)
    {
    }
}