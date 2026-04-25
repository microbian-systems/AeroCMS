using Aero.Cms.Core;
using Aero.Cms.Web.Core.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WorkOS;
using Wos = WorkOS;
using WorkOS = global::WorkOS;
using Aero.Models.Entities;
using Aero.Core.Http;
using Microsoft.Extensions.Logging;
using Polly;
using Serilog;

public class WorkOsModule : AeroModuleBase
{
    public override string Name => nameof(WorkOsModule);

    public override string Version => AeroConstants.Version;

    public override string Author => AeroConstants.Author;

    public override IReadOnlyList<string> Dependencies => [];

    public override IReadOnlyList<string> Category => [];

    public override IReadOnlyList<string> Tags => [];

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


public sealed class WorkOsService(WorkOSClient client)
{
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


public sealed class WorkOsHttpClient : HttpClientBase
{
    public WorkOsHttpClient(HttpClient httpClient, ILogger<HttpClientBase> logger)
        : base(httpClient, logger)
    {
    }
}
