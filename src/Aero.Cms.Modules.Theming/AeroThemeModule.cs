using Aero.Cms.Core;
using Aero.Cms.Modules.Theming.Areas.Api.v1;
using Aero.Cms.Web.Core.Modules;
using Aero.Modular;
using Microsoft.AspNetCore.Routing;

namespace Aero.Cms.Modules.Theming;

[Module(nameof(AeroThemeModule))]
public class AeroThemeModule : AeroWebModule
{
    public override string Name { get; } = nameof(AeroThemeModule);
    public override string Version { get; } = AeroConstants.Version;
    public override string Author { get; } = AeroConstants.Author;
    public override IReadOnlyList<string> Dependencies { get; } = [];
    public override IReadOnlyList<string> Category { get; } = ["theme", "themes", "ui"];
    public override IReadOnlyList<string> Tags { get; } = ["themes", "theme", "ui"];

    public override Task RunAsync(IEndpointRouteBuilder builder)
    {
        builder.MapThemesApi();
        return Task.CompletedTask;
    }
}
