using Aero.Cms.Core;
using Aero.Cms.Modules.Theming.Areas.Api.v1;
using Aero.Cms.Web.Core.Modules;
using Aero.Modular;
using Microsoft.AspNetCore.Routing;

namespace Aero.Cms.Modules.Theming;

/// <summary>
/// Registers the administrative theme discovery and placeholder mutation endpoints.
/// </summary>
[Module(nameof(AeroThemeModule))]
public class AeroThemeModule : AeroWebModule
{
    /// <inheritdoc />
public override string Name { get; } = nameof(AeroThemeModule);
    /// <inheritdoc />
public override string Version { get; } = AeroConstants.Version;
    /// <inheritdoc />
public override string Author { get; } = AeroConstants.Author;
    /// <inheritdoc />
public override IReadOnlyList<string> Dependencies { get; } = [];
    /// <inheritdoc />
public override IReadOnlyList<string> Category { get; } = ["theme", "themes", "ui"];
    /// <inheritdoc />
public override IReadOnlyList<string> Tags { get; } = ["themes", "theme", "ui"];

    /// <summary>
    /// Maps theme endpoints during module startup.
    /// </summary>
    /// <param name="builder">The host endpoint route builder.</param>
    /// <returns>A completed task after synchronous route registration.</returns>
public override Task RunAsync(IEndpointRouteBuilder builder)
    {
        builder.MapThemesApi();
        return Task.CompletedTask;
    }
}
