using Aero.Cms.Core;
using Aero.Cms.Web.Core.Modules;
using Aero.Modular;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Profiling.Storage;

namespace Aero.Cms.Modules.MiniProfiler;

/// <summary>
/// Conditionally registers and inserts MiniProfiler request diagnostics into an Aero CMS web application.
/// </summary>
/// <remarks>
/// Registration is controlled solely by <c>AeroCms:Modules:MiniProfiler:Enable</c>, which defaults to
/// <see langword="false"/> when configuration is absent. This module does not apply environment gating,
/// authorization callbacks, request sampling, or a user-ID provider. Enabling it therefore does not by itself
/// establish production-safe access control for profiler results or UI resources.
/// </remarks>
[Module(nameof(MiniProfilerModule))]
public class MiniProfilerModule : AeroWebModule, IAeroPipelineModule
{
    /// <summary>Gets the fixed name used to discover this module.</summary>
    public override string Name => nameof(MiniProfilerModule);

    /// <summary>Gets the Aero CMS version reported by this module.</summary>
    public override string Version => AeroConstants.Version;

    /// <summary>Gets the Aero CMS author metadata reported by this module.</summary>
    public override string Author => AeroConstants.Author;

    /// <summary>Gets an empty module dependency list.</summary>
    public override IReadOnlyList<string> Dependencies => [];

    /// <summary>Gets the profiler and performance discovery categories.</summary>
    public override IReadOnlyList<string> Category => ["profiler", "performance"];

    /// <summary>Gets the profiler and performance discovery tags.</summary>
    public override IReadOnlyList<string> Tags => ["profiler", "performance"];

    /// <summary>
    /// Registers MiniProfiler and this startup filter when the module enable flag is true.
    /// </summary>
    /// <param name="services">The application service collection to modify.</param>
    /// <param name="config">Configuration used to read the module enable flag.</param>
    /// <param name="env">The host environment forwarded to the base module; it is not used to gate profiling.</param>
    /// <remarks>
    /// Enabled registration uses the <c>/profiler</c> route base, in-memory result storage with a 60-minute cache
    /// duration, connection open/close tracking, MVC filter and view profiling, dark popup rendering at bottom-left,
    /// one decimal timing precision, and the inline SQL formatter. No authorization or sampling delegate is
    /// configured. Registration failures propagate to the caller.
    /// </remarks>
    public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        var enabled = config?.GetValue<bool>("AeroCms:Modules:MiniProfiler:Enable") ?? false;
        if (enabled)
        {
            //services.AddMiniProfiler(options =>
            //{
            //    options.RouteBasePath = "/_profiler";
            //    options.PopupRenderPosition = StackExchange.Profiling.RenderPosition.BottomLeft;
            //    options.PopupShowTimeWithChildren = true;
            //}).AddEntityFramework();
            services.AddMiniProfiler(options =>
            {
                // All of this is optional. You can simply call .AddMiniProfiler() for all defaults

                // (Optional) Path to use for profiler URLs, default is /mini-profiler-resources
                options.RouteBasePath = "/profiler";
                options.PopupRenderPosition = RenderPosition.BottomLeft;
                options.PopupShowTimeWithChildren = true;

                // (Optional) Control storage
                // (default is 30 minutes in MemoryCacheStorage)
                // Note: MiniProfiler will not work if a SizeLimit is set on MemoryCache!
                //   See: https://github.com/MiniProfiler/dotnet/issues/501 for details
                (options.Storage as MemoryCacheStorage).CacheDuration = TimeSpan.FromMinutes(60);

                // (Optional) Control which SQL formatter to use, InlineFormatter is the default
                options.SqlFormatter = new StackExchange.Profiling.SqlFormatters.InlineFormatter();

                // (Optional) To control authorization, you can use the Func<HttpRequest, bool> options:
                // (default is everyone can access profilers)
                //options.ResultsAuthorize = request => MyGetUserFunction(request).CanSeeMiniProfiler;
                //options.ResultsListAuthorize = request => MyGetUserFunction(request).CanSeeMiniProfiler;
                // Or, there are async versions available:
                //options.ResultsAuthorizeAsync = async request => (await MyGetUserFunctionAsync(request)).CanSeeMiniProfiler;
                //options.ResultsListAuthorizeAsync = async request => (await MyGetUserFunctionAsync(request)).CanSeeMiniProfilerLists;

                // (Optional)  To control which requests are profiled, use the Func<HttpRequest, bool> option:
                // (default is everything should be profiled)
                //options.ShouldProfile = request => MyShouldThisBeProfiledFunction(request);

                // (Optional) Profiles are stored under a user ID, function to get it:
                // (default is null, since above methods don't use it by default)
                //options.UserIdProvider = request => MyGetUserIdFunction(request);

                // (Optional) Swap out the entire profiler provider, if you want
                // (default handles async and works fine for almost all applications)
                //options.ProfilerProvider = new MyProfilerProvider();

                // (Optional) You can disable "Connection Open()", "Connection Close()" (and async variant) tracking.
                // (defaults to true, and connection opening/closing is tracked)
                options.TrackConnectionOpenClose = true;

                // (Optional) Use something other than the "light" color scheme.
                // (defaults to "light")
                options.ColorScheme = StackExchange.Profiling.ColorScheme.Dark;

                // Optionally change the number of decimal places shown for millisecond timings.
                // (defaults to 2)
                options.PopupDecimalPlaces = 1;

                // The below are newer options, available in .NET Core 3.0 and above:

                // (Optional) You can disable MVC filter profiling
                // (defaults to true, and filters are profiled)
                options.EnableMvcFilterProfiling = true;
                // ...or only save filters that take over a certain millisecond duration (including their children)
                // (defaults to null, and all filters are profiled)
                // options.MvcFilterMinimumSaveMs = 1.0m;

                // (Optional) You can disable MVC view profiling
                // (defaults to true, and views are profiled)
                options.EnableMvcViewProfiling = true;
                // ...or only save views that take over a certain millisecond duration (including their children)
                // (defaults to null, and all views are profiled)
                // options.MvcViewMinimumSaveMs = 1.0m;

                // (Optional) listen to any errors that occur within MiniProfiler itself
                // options.OnInternalError = e => MyExceptionLogger(e);

                // (Optional - not recommended) You can enable a heavy debug mode with stacks and tooltips when using memory storage
                // It has a lot of overhead vs. normal profiling and should only be used with that in mind
                // (defaults to false, debug/heavy mode is off)
                //options.EnableDebugMode = true;

            });
        }
        
        base.ConfigureServices(services, config, env);
    }

    /// <summary>
    /// Creates the startup-filter delegate that conditionally adds MiniProfiler middleware before the next stage.
    /// </summary>
    /// <param name="next">The remaining application-startup configuration delegate.</param>
    /// <returns>
    /// A delegate that re-reads the enable flag, calls <c>UseMiniProfiler</c> when true, and then invokes
    /// <paramref name="next"/>.
    /// </returns>
    /// <remarks>
    /// Middleware setup is synchronous and exposes no cancellation token. Configuration, middleware-registration,
    /// and downstream-startup exceptions are not caught by this module.
    /// </remarks>
    public int PipelineOrder => 50;

    /// <inheritdoc />
    public void ConfigurePipeline(IApplicationBuilder app)
    {
        var enabled = app.ApplicationServices.GetService<IConfiguration>()?
            .GetValue<bool>("AeroCms:Modules:MiniProfiler:Enable") ?? false;
        if (enabled)
        {
            app.UseMiniProfiler();
        }
    }
}
