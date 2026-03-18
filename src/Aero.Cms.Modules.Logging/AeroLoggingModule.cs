using Aero.Cms.Core.Modules;

namespace Aero.Cms.Modules.Logging;

public class AeroLoggingModule : ModuleBase
{
    public override string Name => "Aero Logging";
    public override string Version => "1.0.0";
    public override string Author => "Aero.Cms";
    public override IReadOnlyList<string> Dependencies => Array.Empty<string>();
}
