using Aero.Cms.Core;
using Aero.Cms.Web.Core.Modules;
using Aero.Modular;

namespace Aero.Cms.Modules.Theming;

public class AeroThemeModule : AeroModuleBase
{
    public override string Name { get; } = nameof(AeroThemeModule);
    public override string Version { get; } = AeroConstants.Version;
    public override string Author { get; } = AeroConstants.Author;
    public override IReadOnlyList<string> Dependencies { get; } = [];
    public override IReadOnlyList<string> Category { get; } = ["theme", "themes", "ui"];
    public override IReadOnlyList<string> Tags { get; } = ["themes", "theme", "ui"];
}
