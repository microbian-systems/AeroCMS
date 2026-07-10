using Aero.Cms.Core;
using Aero.Cms.Modules.Theming.Areas.Api.v1;
using Aero.Cms.Web.Core.Modules;
using Aero.Modular;
using Microsoft.AspNetCore.Routing;

namespace Aero.Cms.Modules.Theming;

/// <summary>
/// Represents a class for AeroThemeModule.
/// </summary>
[Module(nameof(AeroThemeModule))]
public class AeroThemeModule : AeroWebModule
{
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public override string Name { get; } = nameof(AeroThemeModule);
        /// <summary>
    /// Gets or sets the Version.
    /// </summary>
public override string Version { get; } = AeroConstants.Version;
        /// <summary>
    /// Gets or sets the Author.
    /// </summary>
public override string Author { get; } = AeroConstants.Author;
        /// <summary>
    /// Gets or sets the Dependencies.
    /// </summary>
public override IReadOnlyList<string> Dependencies { get; } = [];
        /// <summary>
    /// Gets or sets the Category.
    /// </summary>
public override IReadOnlyList<string> Category { get; } = ["theme", "themes", "ui"];
        /// <summary>
    /// Gets or sets the Tags.
    /// </summary>
public override IReadOnlyList<string> Tags { get; } = ["themes", "theme", "ui"];

        /// <summary>
    /// RunAsync method.
    /// </summary>
public override Task RunAsync(IEndpointRouteBuilder builder)
    {
        builder.MapThemesApi();
        return Task.CompletedTask;
    }
}
