using Aero.Cms.Core;

namespace Aero.Cms.Modules.Garnet;

public class GarnetCacheModule : AeroModuleBase
{
    public override string Name { get; } = nameof(GarnetCacheModule);
    public override string Version { get; } = 
    public override string Author { get; }
    public override string Description { get; }
    public override bool Enabled { get; set; }
    public override bool AllowInProduction { get; set; }
    public override IReadOnlyList<string> Categories { get; } = ["caching"];
    public override IReadOnlyList<string> Tags { get; } = ["garnet", "redis", "caching"];
    public override IReadOnlyList<string> Dependencies { get; }
}