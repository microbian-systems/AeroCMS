using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Core;

/// <summary>
/// Defines the contract for an AeroCMS module.
/// </summary>
public interface IAeroModule
{
    string Name { get; }
    string Version { get; }
    string Author { get; }
    string Description { get; }
    bool Enabled { get; set; }
    IReadOnlyList<string> Dependencies { get; }
    void ConfigureServices(IServiceCollection services, IConfiguration config = default);
    void Init(IServiceProvider sp);
    Task InitAsync(IServiceProvider sp) => Task.CompletedTask;
    void Run(IEndpointRouteBuilder app);
    Task RunAsync(IEndpointRouteBuilder app);
    void Configure(IAeroModuleBuilder builder);
}