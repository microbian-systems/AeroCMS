using Aero.Cms.Core;
using Aero.Modular;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.WebOptimizer;

/// <summary>
/// Registers WebOptimizer services and conditionally adds runtime CSS and JavaScript
/// minification assets.
/// </summary>
/// <remarks>
/// <para>
/// In the Production environment, <see cref="ConfigureServices"/> configures WebOptimizer to
/// minify any requested <c>.css</c> file and then any requested <c>.js</c> file. In every other
/// environment, it registers WebOptimizer with an empty asset pipeline. This module defines no
/// bundle routes or ordered bundle source lists, and it does not configure HTML minification,
/// Sass compilation, source maps, custom content types, CDN rewriting, or file providers.
/// </para>
/// <para>
/// Registration and middleware activation are separate. This module does not implement an Aero
/// pipeline-module contract and does not call <c>UseWebOptimizer</c>; a host must add that
/// middleware before static-file handling for these runtime transformations to serve requests.
/// Without that host integration, the registrations do not transform or serve assets.
/// </para>
/// <para>
/// No WebOptimizer caching options are set directly here. The registered provider binds its
/// <c>WebOptimizer</c> configuration section and supplies its own environment-dependent response,
/// memory, and disk-cache defaults. Those caches are WebOptimizer asset caches; this module does
/// not register ASP.NET Core response caching, ASP.NET Core output caching, or a distributed cache.
/// Provider transformation and cache failures are not caught or converted by this module.
/// </para>
/// <para>
/// The WebOptimizer NuGet package contributes build targets that can publish existing
/// <c>obj/WebOptimizerCache/*.cache</c> files and remove that directory during a clean. This class
/// does not prebuild assets or generate bundle files during compilation. Its pipeline uses NuGet
/// components only and introduces no npm or CDN dependency.
/// </para>
/// <para>
/// Minification is not validation or sanitization. This module does not establish content trust,
/// add Content Security Policy headers or nonces, calculate subresource-integrity values, or make
/// transformed scripts and styles safe to execute.
/// </para>
/// </remarks>
[Module(nameof(WebOptimizerModule))]
public class WebOptimizerModule : AeroModuleBase
{
    /// <summary>
    /// Gets the fixed module-discovery name.
    /// </summary>
    public override string Name { get; } = nameof(WebOptimizerModule);

    /// <summary>
    /// Gets the Aero CMS version reported by this module.
    /// </summary>
    public override string Version { get; } = AeroConstants.Version;

    /// <summary>
    /// Gets the Aero CMS author metadata reported by this module.
    /// </summary>
    public override string Author { get; } = AeroConstants.Author;

    /// <summary>
    /// Gets the module names that must load before this module.
    /// </summary>
    /// <remarks>The WebOptimizer module declares no module dependency.</remarks>
    public override IReadOnlyList<string> Dependencies { get; } = [];

    /// <summary>
    /// Gets the module-discovery categories.
    /// </summary>
    public override IReadOnlyList<string> Category { get; } = ["utilities", "web"];

    /// <summary>
    /// Gets the module-discovery tags.
    /// </summary>
    public override IReadOnlyList<string> Tags { get; } = ["utilities", "web"];

    /// <summary>
    /// Registers WebOptimizer and enables its default CSS and JavaScript minifiers only in
    /// Production.
    /// </summary>
    /// <param name="services">The service collection to receive WebOptimizer registrations.</param>
    /// <param name="config">
    /// The module configuration. This implementation does not read this parameter; WebOptimizer
    /// later resolves and binds the host's registered configuration through its own options
    /// services.
    /// </param>
    /// <param name="env">
    /// The host environment used to decide whether the minification assets are added. Despite
    /// the optional signature inherited from the module contract, this implementation requires
    /// a non-null value.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="env"/> is <see langword="null"/>, or <paramref name="services"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// <para>
    /// When <see cref="IHostEnvironment.EnvironmentName"/> is <c>Production</c>, the registration
    /// calls WebOptimizer's all-files CSS minifier followed by its all-files JavaScript minifier.
    /// Assets remain independently addressable by their requested routes; no concatenation order
    /// or bundle output route is established.
    /// </para>
    /// <para>
    /// Staging, Development, and custom environment names still receive the provider services but
    /// no minification assets. The registration builds the asset-pipeline description during
    /// service configuration; actual file reading, transformation, response headers, and
    /// provider caching occur only when an activated WebOptimizer middleware request uses it.
    /// </para>
    /// </remarks>
    public override void ConfigureServices(
        IServiceCollection services,
        IConfiguration config = null,
        IHostEnvironment env = null)
    {
        var minifyIfProduction = env.IsProduction();
        services.AddWebOptimizer(minifyJavaScript: minifyIfProduction, minifyCss: minifyIfProduction);
    }
}
