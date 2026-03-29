

using Aero.Cms.Core;
using Aero.Cms.Web.Core.Modules;

public class WorkOsModule : AeroModuleBase
{
    public override string Name => nameof(WorkOsModule);

    public override string Version => AeroVersion.Version;

    public override string Author => AeroConstants.Author;

    public override IReadOnlyList<string> Dependencies => [];

    public override IReadOnlyList<string> Category => [];

    public override IReadOnlyList<string> Tags => [];
}