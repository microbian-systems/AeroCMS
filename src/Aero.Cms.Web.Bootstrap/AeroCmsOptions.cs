using Aero.Modular;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Wolverine;

namespace Aero.Cms.Web.Bootstrap;

/// <summary>
/// Controls service registration and endpoint-pipeline integration for Aero CMS.
/// </summary>
/// <remarks>
/// These options are consumed while <see cref="AeroCmsExtensions.AddAeroCmsAsync{TProgram}"/> registers
/// services. Boolean endpoint options are evaluated later by <see cref="AeroCmsExtensions.UseAeroCms"/>
/// and <see cref="AeroCmsExtensions.MapAeroCms{TRootComponent}"/>.
/// </remarks>
public sealed class AeroCmsOptions
{
    /// <summary>
    /// Gets or sets the source-generated module descriptors registered for the host.
    /// </summary>
    /// <remarks>
    /// This value is required by <see cref="AeroCmsExtensions.AddAeroCmsAsync{TProgram}"/>.
    /// The descriptors are passed to runtime registration and registered as a singleton service.
    /// </remarks>
    public IReadOnlyList<ModuleDescriptor>? ModuleDescriptors { get; set; }

    /// <summary>
    /// Gets or sets the callback that adds source-generated Wolverine handlers during service registration.
    /// </summary>
    public Action<WolverineOptions>? ConfigureWolverine { get; set; }

    /// <summary>
    /// Gets or sets the callback that adds source-generated Orleans grains during service registration.
    /// </summary>
    public Action<ISiloBuilder>? ConfigureGrains { get; set; }

    /// <summary>
    /// Gets or sets the base URI used to register Aero HTTP clients.
    /// </summary>
    /// <remarks>
    /// When this value is <see langword="null"/>, registration uses the absolute URI in
    /// <c>ApiSettings:BaseUrl</c>, when one is configured.
    /// </remarks>
    public Uri? ApiBaseUri { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the Scalar API reference endpoint is mapped.
    /// </summary>
    /// <value><see langword="true"/> by default.</value>
    public bool EnableScalarApiReference { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether OpenAPI endpoints are mapped.
    /// </summary>
    /// <value><see langword="true"/> by default.</value>
    public bool EnableOpenApi { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether Hydro services are registered and Hydro middleware is
    /// appended after endpoint mapping.
    /// </summary>
    /// <value><see langword="true"/> by default.</value>
    public bool EnableHydro { get; set; } = true;

    /// <summary>
    /// Gets or sets a callback that customizes the Aero CMS application cookie after its defaults are applied.
    /// </summary>
    public Action<CookieAuthenticationOptions>? ConfigureApplicationCookie { get; set; }

    /// <summary>
    /// Gets or sets a callback that adds or changes authorization policies after the
    /// <c>AeroAdmin</c> policy is registered.
    /// </summary>
    public Action<AuthorizationOptions>? ConfigureAuthorization { get; set; }
}
