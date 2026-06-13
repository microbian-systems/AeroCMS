using Aero.Modular;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Wolverine;

namespace Aero.Cms.Web.Bootstrap;

/// <summary>
/// Public package-first options for integrating Aero CMS into an ASP.NET Core host.
/// </summary>
public sealed class AeroCmsOptions
{
    /// <summary>
    /// Gets or sets the source-generated module descriptors for the host.
    /// </summary>
    public IReadOnlyList<ModuleDescriptor>? ModuleDescriptors { get; set; }

    /// <summary>
    /// Gets or sets the source-generated Wolverine handler registration callback.
    /// </summary>
    public Action<WolverineOptions>? ConfigureWolverine { get; set; }

    /// <summary>
    /// Gets or sets the source-generated Orleans grain registration callback.
    /// </summary>
    public Action<ISiloBuilder>? ConfigureGrains { get; set; }

    /// <summary>
    /// Gets or sets an optional base URI for generated Aero HTTP clients.
    /// </summary>
    public Uri? ApiBaseUri { get; set; }

    /// <summary>
    /// Gets or sets whether the Scalar API reference should be mapped.
    /// </summary>
    public bool EnableScalarApiReference { get; set; } = true;

    /// <summary>
    /// Gets or sets whether OpenAPI endpoints should be mapped.
    /// </summary>
    public bool EnableOpenApi { get; set; } = true;

    /// <summary>
    /// Gets or sets whether Hydro should be enabled at the end of the pipeline.
    /// </summary>
    public bool EnableHydro { get; set; } = true;

    /// <summary>
    /// Gets or sets additional cookie configuration for the Aero CMS application cookie.
    /// </summary>
    public Action<CookieAuthenticationOptions>? ConfigureApplicationCookie { get; set; }

    /// <summary>
    /// Gets or sets additional authorization policy configuration.
    /// </summary>
    public Action<AuthorizationOptions>? ConfigureAuthorization { get; set; }
}
